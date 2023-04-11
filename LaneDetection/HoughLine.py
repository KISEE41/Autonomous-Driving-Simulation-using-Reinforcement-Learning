import cv2
import numpy as np

from LearningNetwork.preprocess import IM_Height, IM_Width

observation_space = (IM_Height, IM_Width, 1)
shape = (IM_Width, IM_Height)

def resize(image):
    if (image.shape[1], image.shape[0] )== shape:
        return image
    else: 
        return cv2.resize(image, (IM_Width, IM_Height), interpolation=cv2.INTER_AREA)


def make_points(image, line):
    try:
        slope, intercept = line
    except:
        return None

    y1 = int(image.shape[0])# bottom of the image
    y2 = int(y1*3/5)         # slightly lower than the middle
    x1 = int((y1 - intercept)/slope)
    x2 = int((y2 - intercept)/slope)

    return [x1, y1, x2, y2]


def average_slope_intercept(image, lines):
    left_fit    = []
    right_fit   = []
    if lines is None:
        return None, None
        
    for line in lines:
        for x1, y1, x2, y2 in line:
            fit = np.polyfit((x1,x2), (y1,y2), 1)
            slope = fit[0]
            intercept = fit[1]
            if slope < 0: # y is reversed in image
                left_fit.append((slope, intercept))
            else:
                right_fit.append((slope, intercept))
    # add more weight to longer lines
    left_fit_average  = np.average(left_fit, axis=0)
    right_fit_average = np.average(right_fit, axis=0)
    left_line  = make_points(image, left_fit_average)
    right_line = make_points(image, right_fit_average)
    averaged_lines = [left_line, right_line]

    # return left_line, right_line
    return averaged_lines


def canny(img):
    gray = cv2.cvtColor(img, cv2.COLOR_RGB2GRAY)
    kernel = 5
    blur = cv2.GaussianBlur(img,(kernel, kernel),0)
    canny = cv2.Canny(img, 50, 150)
    
    return canny


def display_lines(img,lines):
    line_image = np.zeros_like(img)
    if lines is not None:
        for line in lines:
            if line is not None:
                x1, y1, x2, y2 = line
                cv2.line(line_image,(x1,y1),(x2,y2),(255,0,0),10)
    return line_image
                

def region_of_interest(canny):
    height = canny.shape[0]
    width = canny.shape[1]
    mask = np.zeros_like(canny)

    height = canny.shape[0]
    width = canny.shape[1]

    polygon = np.array([[
        [0, height], 
        [width, height], 
        [width, height-50],
        [width * 3/5, height * 3/5], 
        [0, height-50],
        ]], dtype=np.int32
    )

    cv2.fillPoly(mask, polygon, 255)
    masked_image = cv2.bitwise_and(canny, mask)
    return masked_image


def get_cordinates(image):
    lane_image = np.copy(image)
    canny_image = canny(lane_image)
    cropped_canny = region_of_interest(canny_image)
    lines = cv2.HoughLinesP(cropped_canny, 2, np.pi/180, 100, np.array([]), minLineLength=40, maxLineGap=5)

    averaged_lines = average_slope_intercept(image, lines)
    left_lane, right_lane = averaged_lines

    if left_lane is not None:    
        xl = left_lane[0]
    else:
        xl = None
    
    if right_lane is not None:    
        xr = right_lane[0]
    else:
        xr = None

    line_image = display_lines(lane_image, averaged_lines)
    combo_image = cv2.addWeighted(lane_image, 0.8, line_image, 1, 0)

    return xl, xr, combo_image


