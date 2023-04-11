import numpy as np

class ReplayBuffer:
    """
    This class is responsible for storing the agent memory.
    stored as: <state, action, reward, next_state>
    Here, the parameters are stored in different variables but 
    can save in same as the collection of tuple.
    """
    def __init__(self, max_size, input_shape, n_actions):
        #mem_size gives the maximum capacity of the buffer
        self.mem_size = max_size 
        #used for starting element of batch -
        #data mustn't be trained twice with same data, so counter is used
        self.mem_cntr = 0

        #state of the agent stored as an np array
        self.state_memory = np.zeros((self.mem_size, *input_shape), 
                                      dtype= np.float32)

        #new_state gives the state after performing the action
        self.new_state_memory = np.zeros((self.mem_size, *input_shape),
                                          dtype=np.float32)

        #action_memory gives the action taken in correspondin state
        self.action_memory = np.zeros(self.mem_size, dtype=np.int64)

        #reward_memory gives the reward given for action performing in that 
        #particular state
        self.reward_memory = np.zeros(self.mem_size, dtype=np.float32)

        #terminal_memory is like a bool representing whether that state is terminal -
        #to episode or not
        self.terminal_memory = np.zeros(self.mem_size, dtype=np.uint8)

    
    def store_transition(self, state, action, reward, state_, done):
        """
        First find out if there is vacant space to store the <s,a,r,s'>
        if not mem_cntr gives the index of the oldest memory to get replaced.
        """
        index = self.mem_cntr % self.mem_size

        self.state_memory[index] = state
        self.action_memory[index] = action
        self.reward_memory[index] = reward
        self.new_state_memory[index] = state_
        self.terminal_memory[index] = done
        self.mem_cntr += 1


    def sample_buffer(self, batch_size):
        """
        Uniformly sample the buffer memory.
        """
        #gives the index of last stored memory
        #if memory buffer is not full, mem_cntr gives the index upto which -
        #buffer is occupied
        max_mem = min(self.mem_cntr, self.mem_size)
        #batch_size determines how many are used to train model -
        #if batch_size is 32, then 32 images are selected 
        # in random from -
        #the index given by max_mem.
        #replace=False means no duplicate data
        batch = np.random.choice(max_mem, batch_size, replace=False)

        #all those data sturctures gives the collection of data
        #like states gives the collection of images(number equal to batch_size) -
        #stored in buffer, and index given by batch
        states = self.state_memory[batch]
        actions = self.action_memory[batch]
        rewards = self.reward_memory[batch]
        states_ = self.new_state_memory[batch]
        dones = self.terminal_memory[batch]

        return states, actions, rewards, states_, dones