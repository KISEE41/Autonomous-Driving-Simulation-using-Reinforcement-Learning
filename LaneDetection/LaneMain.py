import numpy as np
import cv2

from LaneDetection.Calibration import CameraCalibration
from LaneDetection.Thresholding import Thresholding
from LaneDetection.PerspectiveTransformation import PerspectiveTransformation
from LaneDetection.LaneLines import LaneLines


class FindLaneLines:
    _instance = None

    def __init__(self):
        # self.calibration = CameraCalibration()
        self.thresholding = Thresholding()
        self.transform = PerspectiveTransformation()
        self.lanelines = LaneLines()

    def forward(self, img):
        # img = self.calibration.undistort(img)
        img = self.transform.forward(img)
        img = self.thresholding.forward(img)
        return self.lanelines.forward(img)


def FindLane():
    if FindLaneLines._instance is None:
        FindLaneLines._instance = FindLaneLines()
    return FindLaneLines._instance


def SlidingWindow(image):
    LaneLine = FindLane()
    return LaneLine.forward(image)








