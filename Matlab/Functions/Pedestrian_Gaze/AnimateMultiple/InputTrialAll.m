%% Input Trial All
% This script selects the trial to be animated and saves the video in the
% AnimationVideos folder.
% Author: Johnson Mok

function InputTrialAll(trialAnimate, intersect, videoname, titlestr, titlestrLaser)
    name_mainF = 'AnimationVideos';
    if(~exist(name_mainF, 'dir'))       % Create main folder
        mkdir(name_mainF)
    end
    foldername = fullfile(pwd,name_mainF);   % Create string to main folder
    pedestrianGaze = trialAnimate.pe.world;
    pedestrianGAP = trialAnimate.pe.gapAcceptance;
    passengerGaze = trialAnimate.pa.world;
    passengerLook = trialAnimate.pa.distance;
    animateTrialAll(pedestrianGaze.gaze_origin, pedestrianGaze.gaze_dir, pedestrianGAP,...
        passengerGaze.gaze_origin, passengerGaze.gaze_dir,...
        passengerLook, intersect, [foldername,'\',videoname], titlestr, titlestrLaser);
end

