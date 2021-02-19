%% Run Me
% This is the main script to perform analysis on the data.
% Author: Johnson Mok
% Last Updated: 04-02-2021
clc;
clear;
close all;

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

%% Calculations
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


%% Analyze data
gazeTime = analyzeGazeTimeV2(timesgroup); % gazeTime = analyzeGazeTime(timesgroup, pa_distancegroup, pe_distancegroup);
gapAcpt = analyzeGapAcceptance(gapgroup, rbvgroup, pasposgroup, phasesgroup);
phaseData = analyzePhasesGroup(phasesgroup);
pedGazeDirData = analyzePedestrianGazeDirection(pe_rotationgroup, pe_gazeDir);

%% Visualize data (needs reorganization, for now the calculations and visualization is done in the same script/function) 
visualizeGazeTime(gazeTime); % gazeTimePlotter(gazeTime); 
visualizeGapAcceptance(gapAcpt, phaseData);
visulizePhasesGroup(phaseData);
