from collections import deque
import imp
from turtle import left, right
import cv2

from LaneDetection import HoughLine
from LaneDetection import LaneMain

from LearningNetwork.preprocess import preprocess_frame
from LearningNetwork.reward import calculate_reward

from server import Socket


class Environment:
    def __init__(self):
        self.server = Socket("127.0.0.1", 1234)
        self.hough_line_image = None
        self.sliding_window_image = None
        self.state = None


    def step(self, action):
        """
        Interact with the unity environment, i.e, get the image from unity 
        environment, get actions from network and tells environment to act 
        particular action and returns the image after taking action according 
        to which reward is calculated.
        """
        done = False
        self.server.send_state(action)
        state = self.server.get_state()

        reward = 0.0 

        # H refers to the hough line, its the information returned from Hough line transformation
        H_xl, H_xr, self.hough_line_image = HoughLine.get_cordinates(state)

        #S refers to the sliding window, its the information returned from sliding window
        S_xl, S_xr, self.sliding_window_image = LaneMain.SlidingWindow(state)

        self.state = state

        if (H_xl is None and H_xr is None) and (S_xl is None and S_xr is None):
            done = True

        else:
            if H_xl and H_xr:
                left_lane_position = H_xl
                right_lane_position = H_xr

            elif H_xl is None and H_xr is None:
                left_lane_position = S_xl
                right_lane_position = S_xr
            
            else:
                if S_xl and S_xr:
                    left_lane_position = S_xl
                    right_lane_position = S_xr

                elif S_xl is None and S_xr is None:
                    left_lane_position = H_xl
                    right_lane_position = H_xr
                
                else:
                    if S_xl is None and S_xr is not None:
                        if H_xl is not None:
                            left_lane_position = H_xl
                            right_lane_position = S_xr

                        else:
                            left_lane_position = None
                            right_lane_position = S_xr
                    
                    elif S_xl is not None and S_xr is None:
                        if H_xr is not None:
                            left_lane_position = S_xl
                            right_lane_position = H_xr

                        else:
                            left_lane_position = S_xl
                            right_lane_position = None

            reward = calculate_reward(left_lane_position, right_lane_position)        

        if not reward:
            reward = -1
            done = True

        state = preprocess_frame(state)

        return state, reward, done, {}


    def reset(self):
        """
        Reset the environment.
        i.e. the agent must start from the begining at which the agent was
        at the beigining of the first episode.
        """
        self.server.send_reset()
        state = self.server.get_state()

        state = preprocess_frame(state)
        
        return state 

    
    def render(self):
        img = cv2.addWeighted(self.image, 0.9, self.sliding_window_image, 0.1)
        image = cv2.addWeighted(img, 0.9, self.sliding_window_image, 0.1)
        cv2.imshow("Rendered lane lines", image)
        cv2.waitKey(1)


    def close(self):
        cv2.destroyAllWindows()
        self.server.send_state(0, 0, 1)
  