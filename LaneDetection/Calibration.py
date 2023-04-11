import numpy as np
import cv2
import glob
import matplotlib.image as mpimg
import matplotlib.pyplot as plt

class CameraCalibration():
    """ Class that calibrate camera using chessboard images.

    Attributes:
        mtx (np.array): Camera matrix 
        dist (np.array): Distortion coefficients
    """
    def __init__(self, image_dir="LaneDetection/chess_boards", nx=9, ny=6):
        """ 
        Parameters:
            image_dir (str): path to folder contains chessboard images
            nx (int): width of chessboard (number of squares)
            ny (int): height of chessboard (number of squares)
        """
        self.image_dir = image_dir
        self.nx = nx
        self.ny = ny
        
        fnames = glob.glob("{}/*".format(self.image_dir))
        objpoints = []
        imgpoints = []
        
        # Coordinates of chessboard's corners in 3D
        objp = np.zeros((self.nx*self.ny, 3), np.float32)
        objp[:,:2] = np.mgrid[0:self.nx, 0:self.ny].T.reshape(-1, 2)
        
        # Go through all chessboard images
        for f in fnames:
            img = mpimg.imread(f)

            # Convert to grayscale image
            gray = cv2.cvtColor(img, cv2.COLOR_RGB2GRAY)

            # Find chessboard corners
            ret, corners = cv2.findChessboardCorners(img, (self.nx, self.ny))
            if ret:
                imgpoints.append(corners)
                objpoints.append(objp)

        shape = (img.shape[1], img.shape[0])
        ret, self.mtx, self.dist, _, _ = cv2.calibrateCamera(objpoints, imgpoints, shape, None, None)

        if not ret:
            raise Exception("Unable to calibrate camera")

    def undistort(self, image):
        """ Return undistort image.

        Parameters:
            img (np.array): Input image

        Returns:
            Image (np.array): Undistorted image
        """
        # Convert to grayscale image
        return cv2.undistort(image, self.mtx, self.dist, None, self.mtx)

