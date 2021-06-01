%% Input Trial All
% This script selects the trial to be animated and saves the video in the
% AnimationVideos folder.
% Author: Johnson Mok

function InputCombined(gap, pa_Dir, pa_Org, pe_Dir, pe_Org, pa_DirMean, pa_OrgMean, pe_DirMean, pe_OrgMean, diMat, title)
    name_mainF = 'AnimationVideos';
    if(~exist(name_mainF, 'dir'))       % Create main folder
        mkdir(name_mainF)
    end
    foldername = fullfile(pwd,name_mainF);   % Create string to main folder
    videoname = [foldername,'\',title,'.avi'];
    animateTrialsCombined(gap, pa_Dir, pa_Org, pe_Dir, pe_Org, pa_DirMean, pa_OrgMean, pe_DirMean, pe_OrgMean, diMat, title, videoname) 
end

