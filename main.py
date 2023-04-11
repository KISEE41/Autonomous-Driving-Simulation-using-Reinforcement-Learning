from util import plot_learning_curve
from argsParser import argsParser
from LearningNetwork.preprocess import shape
from LearningNetwork.environment import Environment
from LearningNetwork import ddqn_agent
from LearningNetwork import dqn_agent
from torch.utils.tensorboard import SummaryWriter
import numpy as np
import os
import json

import warnings
warnings.filterwarnings(action='ignore', message='Mean of empty slice')
warnings.filterwarnings(
    action='ignore', message='invalid value encountered in double_scalars')
warnings.filterwarnings(
    action='ignore', message='divide by zero encountered in double_scalars')


os.environ['KMP_DUPLICATE_LIB_OK'] = 'True'


if __name__ == "__main__":
    args = argsParser()
    os.environ['CUDA_VISIBLE_DEVICES'] = args.gpu

    env = Environment()  # creation of environment

    if args.algo == "DDQNAgent":
        Agent = getattr(ddqn_agent, args.algo)
        writer = SummaryWriter("runs/" + args.algo)
    elif args.algo == "DQNAgent":
        Agent = getattr(dqn_agent, args.algo)
        writer = SummaryWriter("runs/" + args.algo)
    else:
        print("Wrong Algorithm specification. Specify DQNAgent or DDQNAgent")

    agent = Agent(gamma=args.gamma,
                  epsilon=args.eps,
                  lr=args.lr,
                  input_dims=shape,
                  n_actions=4,
                  mem_size=args.max_mem,
                  eps_min=args.eps_min,
                  batch_size=args.bs,
                  replace=args.replace,
                  eps_dec=args.eps_dec,
                  chkpt_dir=args.path,
                  algo=args.algo)

    # best_score = 0
    # n_steps = 0
    # scores, eps_history, step_array = [], [], []

    fname = args.algo + '_lr' + \
        str(args.lr) + '_' + str(args.n_games) + 'games'
    figure_file = 'plots/' + fname + '.png'

    if args.load_checkpoint:
        print("-----------Loading model-----------")
        agent.load_models()
        parameters = open('parameters.json')
        parameters = json.load(parameters)
        n_steps = parameters['n_steps']
        best_score = parameters['best_score']
        agent.epsilon = parameters['epsilon']
    else:
        n_steps = 0
        best_score = 0
        epsilon = 1
        parameters = {
            'n_steps': n_steps,
            'epsilon': epsilon,
            'best_score': best_score,
            'scores': [],
            'eps_history': [],
            'step_array': []
        }

    load_checkpoint = False

    print('episode 0', 'score: 0', 'average score %.1f best score %.1f epsilon %.2f' % (
        0, best_score, agent.epsilon), 'steps', n_steps)
    for i in range(args.n_games):
        done = False
        score = 0
        observation = env.reset()
        n_steps_eps = 0

        while not done:
            action = agent.choose_action(observation)
            observation_, reward, done, info = env.step(action)
            score += reward

            if n_steps == 0 or n_steps % 100 == 0:
                print(
                    f"--->Total steps: {n_steps}, steps in an episode: {n_steps_eps}, reward obtained: {reward}, epsilon: {agent.epsilon}")
                parameters['n_steps'] = n_steps
                parameters['epsilon'] = agent.epsilon
                parameters['best_score'] = best_score
                parameters['scores'].append(score)
                parameters['eps_history'].append(agent.epsilon)
                parameters['step_array'].append(n_steps)

                writer.add_scalar("Reward", np.mean(
                    parameters["scores"][-100:]), global_step=n_steps)
                writer.add_scalar("Epsilon", np.mean(
                    parameters["eps_history"][-100:]), global_step=n_steps)
                json.dump(parameters, open('parameters.json', 'w'), indent=4)

                avg_score = score/100
                if avg_score > best_score:
                    if not load_checkpoint:
                        print("--------Best score detected, saving model-----------")
                        agent.save_models()
                    best_score = avg_score

                score = 0

            if not load_checkpoint:
                agent.store_transition(
                    observation, action, reward, observation_, int(done))
                agent.learn()

            observation = observation_
            n_steps_eps += 1
            n_steps += 1

        avg_score = np.mean(parameters["scores"][-n_steps_eps:])
        print('episode', i+1, 'score:', score, 'average score of an episode %.1f best score %.1f epsilon %.2f' %
              (avg_score, best_score, agent.epsilon), 'steps', n_steps)

    writer.close()
    plot_learning_curve(
        parameters['step_array'], parameters['scores'], parameters['eps_history'], figure_file)
    env.close()
