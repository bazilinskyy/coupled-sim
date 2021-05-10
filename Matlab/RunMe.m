%% Run Me
% This is the main script to perform analysis on the data.
% Author: Johnson Mok
clc;
clear;
close all;

%% Inputs
createAnimation = false;
showPlot = false;
showWrong = false;

%% Add path to functions
addpath(genpath('Functions'));
addpath('Mats');
addpath('Questionnaires');

%% Load Data 
if(~exist('AllData.mat'))
% Notice: depending on the number and size of the logfiles, it will take multiple hours to create the datastructure. 
    run('FileOrdener.m');       % Order all files from log data to the file structure.
    run('DataStructCreator.m'); % Convert CSV files to matlab data structure and save it.
end
if(~exist('PreData.mat'))
    load('AllData.mat');                % Load pre-converted data
    PreData = PreProcess(ParentData);   % Correct faulty data
	save('PreData.mat','PreData', '-v7.3');
end
if(~exist('PreDataV2.mat'))
    run('SetStartAndEnd.m');            % Remove data before start signal and after 2.6 s at standstill
end
if(~exist('WrongData.mat'))
    load('PreDataV2.mat');
    [PreDataV3, WrongData] = FilterWrongExecutions(PreDataV2); % Filter wrongly executed trials
	save('PreDataV3.mat','PreDataV3', '-v7.3');
    save('WrongData.mat','WrongData', '-v7.3');
end
if(exist('PreDataV3.mat'))
    load('PreData.mat');
    load('PreDataV3.mat');   
    load('WrongData.mat');
end

load('Acceptance_pa.mat');  % Run the script 'Qustionnaire_analyse_post_block' to get these two mats
load('Acceptance_pe.mat');

%% Number of data filtered
if (showWrong == true)
    VisualizeDataFiltered(WrongData);
end

%% Calculations
phasesV2    = calcPhasesV2(PreDataV3);
timesV2     = CalcTimeV2(PreDataV3, PreData, phasesV2);

%% Create grouped data
timesgroupV2	= createGroupData(timesV2, 'time');
gapgroup        = createGroupData(PreDataV3, 'gap');
rbvgroup        = createGroupData(PreDataV3, 'rb_v');
pasposgroup     = createGroupData(PreDataV3, 'pa_pos');
phasesgroupV2	= createGroupData(phasesV2, 'phases');
pe_gazeDir      = createGroupData(PreDataV3, 'HMDgaze_dir');
pe_gazeOrg      = createGroupData(PreDataV3, 'HMDgaze_org');
participantTrialGroup = createGroupData(PreDataV3, 'trialorder');
gapOrderGroup	= OrderByTrialAll(PreDataV3);

%% Analyze data
trialorder  = analyzeTrialOrder(participantTrialGroup);
gazeTimeV2  = analyzeGazeTimeV2(timesgroupV2, trialorder); 
gapAcptV2   = analyzeGapAcceptance(gapgroup, rbvgroup, pasposgroup, phasesgroupV2, trialorder);
phaseDataV2 = analyzePhasesGroup(phasesgroupV2);
learnEffect = analyzeLearningEffect(gapOrderGroup);
peHeadAngleV2 = analyzePedestrianGazeAngleV2(pe_gazeOrg, pe_gazeDir, phasesgroupV2, trialorder);
crossPerformance = crossingPerformance(gapAcptV2, Acceptance_pa, Acceptance_pe, trialorder);
r = correlationPerformance(crossPerformance.SPSS, gapAcptV2.SPSS);

%% Animation
% Modify 'trialAnimate' to change the trial to animate
% Modify the 'videoname' AND 'titlestr' accordingly.
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

%% Visualize data
if showPlot == true
    trajectoryDiAV(PreDataV3);
    visualizeGapAcceptanceV2(gapAcptV2, phaseDataV2);
    visualizeLearnEffect(learnEffect);
    visualizeHeadAngle(peHeadAngleV2);
    visualizePhasesGroupV2(phaseDataV2);
    visualizeCrossingPerformance(crossPerformance);
    visualizeEyeContact(gazeTimeV2, crossPerformance)
end
