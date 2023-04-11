import cv2
import numpy as np

IM_Height = 200
IM_Width = 500
observation_space = (IM_Height, IM_Width, 1)

shape = (observation_space[2], observation_space[0], observation_space[1])


def preprocess_frame(image):
    """
    This class is responsible for reshaping the image to particular shape.
    And integrate the concept of Hough Line or sliding widow for detecting
    lane.
    """
    #pytorch takes 1st parameter as channel
    new_frame = cv2.cvtColor(image, cv2.COLOR_RGB2GRAY)
    resized_image = cv2.resize(new_frame, shape[1:], interpolation=cv2.INTER_AREA)

    new_obs = np.array(resized_image, dtype=np.uint8).reshape(shape)
    new_obs = new_obs / 255.0

    return new_obs 