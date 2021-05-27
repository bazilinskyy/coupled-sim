%% Run Me
% This is the main script to perform analysis on the data.
% Author: Johnson Mok
clc;
clear;
close all;

%% Inputs
createAnimation = false;
createAnimationLaserCheck = false;
createAnimationCombined = true;
showPlot = false;
showWrong = false;
showSA = false;

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
pe_dis_group    = createGroupData(PreDataV3, 'pe_distance');
rbvgroup        = createGroupData(PreDataV3, 'rb_v');
pasposgroup     = createGroupData(PreDataV3, 'pa_pos');
phasesgroupV2	= createGroupData(phasesV2, 'phases');
pe_gazeDir      = createGroupData(PreDataV3, 'HMDgaze_dir');
pe_gazeOrg      = createGroupData(PreDataV3, 'HMDgaze_org');
pa_gazeDir      = createGroupData(PreDataV3, 'HMDgaze_dir_pa');
pa_gazeOrg      = createGroupData(PreDataV3, 'HMDgaze_org_pa');
participantTrialGroup = createGroupData(PreDataV3, 'trialorder');
pa_world_gazeDir	= createGroupData(PreDataV3, 'pa_world_gaze_dir');
pa_world_gazeOrg	= createGroupData(PreDataV3, 'pa_world_gaze_org');
pe_world_gazeDir	= createGroupData(PreDataV3, 'pe_world_gaze_dir');
pe_world_gazeOrg	= createGroupData(PreDataV3, 'pe_world_gaze_org');
gapOrderGroup	= OrderByTrialAll(PreDataV3);
di_pos = createGroupData(PreDataV3, 'di_pas');

%% Analyze data
trialorder  = analyzeTrialOrder(participantTrialGroup);
gazeTimeV2  = analyzeGazeTimeV2(timesgroupV2, trialorder); 
gapAcptV2   = analyzeGapAcceptance(gapgroup, rbvgroup, pasposgroup, phasesgroupV2, trialorder);
phaseDataV2 = analyzePhasesGroup(phasesgroupV2);
learnEffect = analyzeLearningEffect(gapOrderGroup);
peHeadAngleV2 = analyzePedestrianGazeAngleV2(pe_world_gazeOrg, pe_world_gazeDir, phasesgroupV2, trialorder);
paHeadAngle = analyzeDriverGazeAngle(pa_world_gazeOrg, pa_world_gazeDir, phasesgroupV2, trialorder);

crossPerformance = crossingPerformance(gapAcptV2, Acceptance_pa, Acceptance_pe, trialorder);
r = correlationPerformance(crossPerformance.SPSS, gapAcptV2.SPSS);

%% 3D line intersection
PeGazeAtAV = GazeAtAVAll(pe_dis_group);
[GazeLaser_dis,GazeLaser_ind] = GazeAtLaserAll(pe_world_gazeOrg, pe_world_gazeDir, pa_world_gazeOrg, pa_world_gazeDir, pasposgroup);

%% 2D line intersection
clc; close all;
intersect2D = GazeAtLaser2D(PreDataV3);

%% Animation
% Note: Must be in rootfolder
% Modify 'trialAnimate' to change the trial to animate
% Modify the 'videoname' AND 'titlestr' accordingly.

if createAnimation == true % Animation for one trial
    clc
    close all
    InputTrial(PreDataV3.Data_ED_4.HostFixedTimeLog.participant_1.trial_0, 'ED_4_participant_1_trial_0_VE1.avi', 'GTY - Yielding');
end

if createAnimationLaserCheck == true % Animation for randomly selected trials
    clc
    close all
    AnimateAllTrials(PreDataV3,intersect2D);
end

if createAnimationCombined == true % Animation for all trials (per condition and mapping) in one figure.
   clc
   close all
   AnimateCombined(pa_world_gazeDir, pa_world_gazeOrg, pe_world_gazeDir, pe_world_gazeOrg, di_pos);
end

%% Visualize data
if showPlot == true
    trajectoryDiAV(PreDataV3);
    visualizeGapAcceptanceV2(gapAcptV2, phaseDataV2);
    visualizeLearnEffect(learnEffect);
    visualizeHeadAngle(peHeadAngleV2);
    visualizeHeadAngle_Driver(paHeadAngle);
    visualizePhasesGroupV2(phaseDataV2);
    visualizeCrossingPerformance(crossPerformance);
    visualizeEyeContact(gazeTimeV2, crossPerformance);
end
% close all; clc;
%     visualizeHeadAngle(peHeadAngleV2);

%% Display statistical analysis
if showSA == true
    % Crossing performance
    disp("StatisticalAnalysis_CrossingPerformance_D_NY:"); disp(crossPerformance.StatisticalAnalysis_CrossingPerformance_D_NY);
    disp("StatisticalAnalysis_CrossingPerformance_D_Y:"); disp(crossPerformance.StatisticalAnalysis_CrossingPerformance_D_Y);
    disp("StatisticalAnalysis_CrossingPerformance_ND_NY:"); disp(crossPerformance.StatisticalAnalysis_CrossingPerformance_ND_NY);
    disp("StatisticalAnalysis_CrossingPerformance_ND_Y:"); disp(crossPerformance.StatisticalAnalysis_CrossingPerformance_ND_Y);
    disp("StatisticalAnalysis_CrossingPerformance_Cohen:"); disp(crossPerformance.StatisticalAnalysis_CrossingPerformance_Cohen);

    % Gaze yaw
    disp("StatisticalAnalysis_yaw_D_NY:"); disp(peHeadAngleV2.StatisticalAnalysis_yaw_D_NY);
    disp("StatisticalAnalysis_yaw_D_Y:"); disp(peHeadAngleV2.StatisticalAnalysis_yaw_D_Y);
    disp("StatisticalAnalysis_yaw_ND_NY:"); disp(peHeadAngleV2.StatisticalAnalysis_yaw_ND_NY);
    disp("StatisticalAnalysis_yaw_ND_Y:"); disp(peHeadAngleV2.StatisticalAnalysis_yaw_ND_Y);
    disp("StatisticalAnalysis_yaw_Cohen:"); disp(peHeadAngleV2.StatisticalAnalysis_yaw_Cohen);
    
    % Decision certainty
    disp("StatisticalAnalysis_DC_D_NY:"); disp(gapAcptV2.StatisticalAnalysis_DC_D_NY);
    disp("StatisticalAnalysis_DC_D_Y:"); disp(gapAcptV2.StatisticalAnalysis_DC_D_Y);
    disp("StatisticalAnalysis_DC_ND_NY:"); disp(gapAcptV2.StatisticalAnalysis_DC_ND_NY);
    disp("StatisticalAnalysis_DC_ND_Y:"); disp(gapAcptV2.StatisticalAnalysis_DC_ND_Y);
    disp("StatisticalAnalysis_DC_Cohen:"); disp(gapAcptV2.StatisticalAnalysis_DC_Cohen);
    
    disp("StatisticalAnalysis_Correlation_CrossingPerformance_DC"); disp(r.Correlation_crossPerformance_DC);
end


