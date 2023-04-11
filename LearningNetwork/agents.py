import numpy as np
import json
import torch as T

from LearningNetwork.Qnetwork import DeepQNetwork
from LearningNetwork.replayBuffer import ReplayBuffer


class Agent:
    def __init__(self, gamma, epsilon, lr, n_actions, input_dims, mem_size, batch_size,
                eps_min=0.01, eps_dec=5e-7, replace=1000, algo=None, chkpt_dir='models/'):
        self.gamma = gamma
        self.epsilon = epsilon
        self.lr = lr
        self.n_actions = n_actions
        self.batch_size = batch_size
        self.input_dims = input_dims
        self.eps_min = eps_min
        self.eps_dec = eps_dec
        #evey replace time agent call learn, agent replace target network weight with evalution network's weight
        self.replace_target_cnt = replace 
        self.algo = algo
        self.chkpt_dir = chkpt_dir

        self.action_space = [i for i in range(self.n_actions)]

        #counter to track whether it is time to replace target network
        self.learn_step_counter = 0

        self.memory = ReplayBuffer(mem_size, input_dims, n_actions)

    
    def choose_action(self, observation):
        #epsilon greedy action selection
        if np.random.random() > self.epsilon:
            #wrapping the observation in a list is because to add new dimension in front to make it similar to batch_size * num_channel * image_height * im_width
            # state = T.tensor([observation], dtype=T.float).to(self.q_eval.device)
            state = T.tensor(np.asarray([observation]), dtype=T.float).to(self.q_eval.device)  
            actions = self.q_eval.forward(state)
            action = T.argmax(actions).item()

        else:
            action = np.random.choice(self.action_space)

        return action


    #agents memory
    def store_transition(self, state, action, reward, state_, done):
        self.memory.store_transition(state, action, reward, state_, done)

    
    #sample memory to train the evaluation network in batch
    def sample_memory(self):
        state, action, reward, new_state, done = self.memory.sample_buffer(self.batch_size)
    
        states = T.tensor(state).to(self.q_eval.device)
        rewards = T.tensor(reward).to(self.q_eval.device)
        dones = T.tensor(done).to(self.q_eval.device)
        actions = T.tensor(action).to(self.q_eval.device)
        states_ = T.tensor(new_state).to(self.q_eval.device)

        return states, actions, rewards, states_, dones

    
    #replacing target network
    def replace_target_network(self):
        if self.learn_step_counter % self.replace_target_cnt == 0:
            self.q_next.load_state_dict(self.q_eval.state_dict())

    
    #decrementing epsilon
    def decrement_epsilon(self):
        self.epsilon = self.epsilon - self.eps_dec if self.epsilon > self.eps_min else self.eps_min

    
    def save_models(self):
        self.q_eval.save_checkpoint()
        self.q_next.save_checkpoint()


    def load_models(self):
        self.q_eval.load_checkpoint()
        self.q_next.load_checkpoint()
    
    def learn(self):
        raise NotImplementedError