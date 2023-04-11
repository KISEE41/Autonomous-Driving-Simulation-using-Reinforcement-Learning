import argparse


def argsParser():
    parser = argparse.ArgumentParser(description="Choose which algorithm to use:\
                                                        -DDQN: Double Deep Q Network\
                                                        -DQN: Deep Q Network\
                                                    And choose the hyper parametes.")

    # the hyphen makes the argument optional
    parser.add_argument("-n_games", type=int, default=1000,
                        help="Number of episodes.")

    parser.add_argument("-lr", type=float, default=0.0001,
                        help="Learning rate for optimizer.")

    parser.add_argument("-eps_min", type=float, default=0.1,
                        help="Minimum value for epsilon in epsilon greedy action selection.")

    parser.add_argument("-gamma", type=float, default=0.99,
                        help="Discount factor for update equation.")

    parser.add_argument("-eps_dec", type=float, default=1e-5,
                        help="Linear factor for decreasing epsilon")

    parser.add_argument("-eps", type=float, default=1.0,
                        help="Starting value for epsilon in epsilon-greedy action selection")

    parser.add_argument("-max_mem", type=int, default=50,
                        help="Maximum size for memory replay buffer")

    parser.add_argument('-bs', type=int, default=4,
                        help="Batch size for replay memory sampling")

    parser.add_argument("-replace", type=int, default=50,
                        help="interval for replacing target network")

    parser.add_argument("-gpu", type=str, default='0', help="GPU: 0 or 1")

    parser.add_argument("-load_checkpoint", type=bool,
                        default=False, help='load model checkpoint')

    parser.add_argument("-path", type=str, default="models/",
                        help="path for model saving/loading")

    parser.add_argument("-algo", type=str, default="DQNAgent",
                        help="DQNAgent/DDQNAgent")

    args = parser.parse_args()

    return args
