from LearningNetwork.preprocess import IM_Width


carPosition = IM_Width / 2
carTolerance = 0


def calculate_reward(left_lane_coordinate, right_lane_coordinate):
    cte_max = 0

    if left_lane_coordinate and right_lane_coordinate:
        middle_of_lane = (right_lane_coordinate - left_lane_coordinate)/2
        cte = abs(carPosition - middle_of_lane)
        cte_max = middle_of_lane - carTolerance

    else:
        cte_max = 200

        if left_lane_coordinate is not None and right_lane_coordinate is None:
            if left_lane_coordinate > carPosition:
                return -1
            
            else:
                cte = carPosition - left_lane_coordinate

        if left_lane_coordinate is None and right_lane_coordinate is not None:
            if right_lane_coordinate < carPosition:
                return -1

            else:
                cte = right_lane_coordinate - carPosition

    
    reward = 1 - (cte/cte_max)

    if reward >= 0 and reward <= 1:
        return reward
    
    else:
        return -1



    

    