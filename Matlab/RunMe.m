%% Run Me
% This is the main script to perform analysis on the data.
% Author: Johnson Mok
% Last Updated: 04-02-2021
clc;
clear;
close all;

%% Inputs
createAnimation = false;
showPlot = false;

%% Add path to functions
addpath(genpath('Functions'));
addpath('Mats');

%% Load Data 
if(~exist('AllData.mat'))
% Notice: depending on the number and size of the logfiles, it will take multiple hours to create the datastructure. 
    run('FileOrdener.m');       % Orden all files from log data to the file structure.
    run('DataStructCreator.m'); % Convert CSV files to matlab data structure and save it.
end
if(~exist('PreData.mat'))
    load('AllData.mat');                % Load pre-converted data
    PreData = PreProcess(ParentData);   % Remove faulty data
	save('PreData.mat','PreData');
end
if(~exist('PreDataV2.mat'))
    load('PreData.mat');
    run('SetStartAndEnd.m');            % Remove data before and after sound signals
end
if(~exist('WrongData.mat'))
    load('PreDataV2.mat');
    [PreDataV3, WrongData] = FilterWrongExecutions(PreDataV2);
	save('PreDataV3.mat','PreDataV3');
    save('WrongData.mat','WrongData');
end
if(exist('PreDataV3.mat'))
    load('PreData.mat');
    load('PreDataV3.mat');   
    load('WrongData.mat');
end

%% test
% close all; clc;
% trajectoryDiAV(PreDataV3);
%% test
% close all; clc;
% analyzeTriggerYield(PreDataV3);


%% Calculations
clc
phases = calcPhases(PreDataV3);
times = CalcTime(PreDataV3, PreData, phases);

% Create grouped data
timesgroup = createGroupData(times, 'time');
gapgroup = createGroupData(PreDataV3, 'gap');
rbvgroup = createGroupData(PreDataV3, 'rb_v');
pasposgroup = createGroupData(PreDataV3, 'pa_pos');
pa_distancegroup = createGroupData(PreDataV3, 'pa_distance');
pe_distancegroup = createGroupData(PreDataV3, 'pe_distance');
phasesgroup = createGroupData(phases, 'phases');
pe_rotationgroup = createGroupData(PreDataV3, 'pe_rotation');
pe_gazeDir = createGroupData(PreDataV3, 'HMDgaze_dir');
pe_gazeOrg = createGroupData(PreDataV3, 'HMDgaze_org');

gapOrderGroup = OrderByTrialAll(PreDataV3);

%% Analyze data
% gazeTime = analyzeGazeTimeV2(timesgroup); % gazeTime = analyzeGazeTime(timesgroup, pa_distancegroup, pe_distancegroup);
% gapAcpt = analyzeGapAcceptance(gapgroup, rbvgroup, pasposgroup, phasesgroup);
phaseData = analyzePhasesGroup(phasesgroup);
% learnEffect = analyzeLearningEffect(gapOrderGroup);
% peHeadAngle = analyzePedestrianGazeAngle(pe_gazeOrg, pe_gazeDir, phasesgroup);
% DC = calcDecisionCertainty(PreDataV3, phases);


%% Animation
if createAnimation == true
    videoname = 'ED_8_participant_1_trial_6';
    titlestr = 'ED\_8\_participant\_1\_trial\_6';
    trialAnimate = PreDataV3.Data_ED_8.HostFixedTimeLog.participant_1.trial_6;
    pedestrianGaze = trialAnimate.pe.world;
    pedestrianGAP = trialAnimate.pe.gapAcceptance;
    passengerGaze = trialAnimate.pa.world;
    passengerLook = trialAnimate.pa.distance;
    animateTrial(pedestrianGaze.gaze_origin, pedestrianGaze.gaze_dir, pedestrianGAP,...
        passengerGaze.gaze_origin, passengerGaze.gaze_dir,...
        passengerLook, videoname, titlestr);
end

%% Visualize data (needs reorganization, for now the calculations and visualization is done in the same script/function) 
if showPlot == true
    visualizeGapAcceptance(gapAcpt, phaseData);
    visualizeLearnEffect(learnEffect);
    visualizeGazeTime(gazeTime);  
    visualizeHeadAngle(peHeadAngle);
    visulizePhasesGroup(phaseData);
end
close all; clc;
        visulizePhasesGroup(phaseData);


    

