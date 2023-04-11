import numpy as np
import torch as T

from LearningNetwork.Qnetwork import DeepQNetwork
from LearningNetwork.replayBuffer import ReplayBuffer

from LearningNetwork.agents import Agent


class DQNAgent(Agent):
    def __init__(self, *args, **kwargs):
        super(DQNAgent, self).__init__(*args, **kwargs)

        #evaluation Q network
        #backpropagation and optimization only occur in evaluation network
        self.q_eval = DeepQNetwork(self.lr, self.n_actions, input_dims=self.input_dims,
                                    name=self.algo + '_q_eval', 
                                    chkpt_dir=self.chkpt_dir)

        #target Q network
        #Backpropagation and optimization doestn't occur
        #But it get replace with the weight of evaluation network in every replace's time agent calls learn function
        self.q_next = DeepQNetwork(self.lr, self.n_actions, input_dims=self.input_dims,
                                    name=self.algo + '_q_target', 
                                    chkpt_dir=self.chkpt_dir)

    
    def learn(self):
        if self.memory.mem_cntr < self.batch_size:
            return
        self.q_eval.optimizer.zero_grad()

        self.replace_target_network()

        states, actions, rewards, states_, dones = self.sample_memory()

        #instead of determining q value of whole batch size, we want the q value of each state
        #we get the dimension of batch_size by num_actions, but we want only the dimension of batch_size
        #so we add another index
        indices = np.arange(self.batch_size)
        q_pred = self.q_eval.forward(states)[indices, actions]

        q_next = self.q_next.forward(states_).max(dim=1)[0]

        # q_next[dones] = 0.0
        if dones.any() == 1:
            q_next = 0.0

        q_target = rewards + self.gamma*q_next

        loss = self.q_eval.loss(q_target, q_pred).to(self.q_eval.device)
        loss.backward()
        self.q_eval.optimizer.step()
        self.learn_step_counter += 1

        self.decrement_epsilon()