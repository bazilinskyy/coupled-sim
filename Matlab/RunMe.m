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

load('Acceptance_pa.mat');
load('Acceptance_pe.mat');

%% test
% close all; clc;
% trajectoryDiAV(PreDataV3);
%% test
% close all; clc;
% analyzeTriggerYield(PreDataV3);
%% Number of data filtered
% VisualizeDataFiltered(WrongData);

%% Calculations
phases = calcPhases(PreDataV3);
phasesV2 = calcPhasesV2(PreDataV3);

times = CalcTime(PreDataV3, PreData, phases);
timesV2 = CalcTimeV2(PreDataV3, PreData, phasesV2);

% Create grouped data
timesgroup = createGroupData(times, 'time');
timesgroupV2 = createGroupData(timesV2, 'time');
gapgroup = createGroupData(PreDataV3, 'gap');
rbvgroup = createGroupData(PreDataV3, 'rb_v');
pasposgroup = createGroupData(PreDataV3, 'pa_pos');
pa_distancegroup = createGroupData(PreDataV3, 'pa_distance');
pe_distancegroup = createGroupData(PreDataV3, 'pe_distance');
phasesgroup = createGroupData(phases, 'phases');
phasesgroupV2 = createGroupData(phasesV2, 'phases');
pe_rotationgroup = createGroupData(PreDataV3, 'pe_rotation');
pe_gazeDir = createGroupData(PreDataV3, 'HMDgaze_dir');
pe_gazeOrg = createGroupData(PreDataV3, 'HMDgaze_org');
participantTrialGroup = createGroupData(PreDataV3, 'trialorder');

gapOrderGroup = OrderByTrialAll(PreDataV3);

%% Analyze data
trialorder = analyzeTrialOrder(participantTrialGroup);
% gazeTime = analyzeGazeTimeV2(timesgroup); % gazeTime = analyzeGazeTime(timesgroup, pa_distancegroup, pe_distancegroup);
gazeTimeV2 = analyzeGazeTimeV2(timesgroupV2); 
% gapAcpt = analyzeGapAcceptance(gapgroup, rbvgroup, pasposgroup, phasesgroup);
gapAcptV2 = analyzeGapAcceptance(gapgroup, rbvgroup, pasposgroup, phasesgroupV2, trialorder);
phaseData = analyzePhasesGroup(phasesgroup);
phaseDataV2 = analyzePhasesGroup(phasesgroupV2);
learnEffect = analyzeLearningEffect(gapOrderGroup);

peHeadAngle = analyzePedestrianGazeAngle(pe_gazeOrg, pe_gazeDir, phasesgroup);
peHeadAngleV2 = analyzePedestrianGazeAngleV2(pe_gazeOrg, pe_gazeDir, phasesgroupV2, trialorder);

DC = calcDecisionCertainty(PreDataV3, phases);
crossPerformance = crossingPerformance(gapAcptV2, Acceptance_pa, Acceptance_pe, trialorder);
%%
r = correlationPerformance(crossPerformance.SPSS, gapAcptV2.SPSS);

%% Animation
clc
close all
if createAnimation == true
    videoname = 'ED_4_participant_1_trial_0_VE1.avi';
    titlestr = 'GTY - Yielding';
    trialAnimate = PreDataV3.Data_ED_4.HostFixedTimeLog.participant_1.trial_0;
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
    visualizeGapAcceptanceV2(gapAcptV2, phaseDataV2);

    visualizeLearnEffect(learnEffect);
    
    visualizeGazeTime(gazeTime);  
    
    visualizeHeadAngle(peHeadAngle);
    visualizeHeadAngle(peHeadAngleV2);
    
    visulizePhasesGroup(phaseData);
    visualizePhasesGroupV2(phaseDataV2);
    
    visualizeCrossingPerformance(crossPerformance);
end

