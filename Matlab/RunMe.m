%% Run Me
% This is the main script to perform analysis on the data.
% Author: Johnson Mok
% Last Updated: 04-02-2021
clc;
clear;
close all;

%% Add path to functions
addpath(genpath('Functions'));
addpath(genpath('Mats'));


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
if(exist('PreDataV2.mat'))
    load('PreData.mat');
    load('PreDataV2.mat');          
end

%% What do we want to do with the data?
%
times = CalcTime(PreDataV2, PreData);
% Create grouped data
timesgroup = createGroupData(times, 'time');
gapgroup = createGroupData(PreDataV2, 'gap');
rbvgroup = createGroupData(PreDataV2, 'rb_v');
pasposgroup = createGroupData(PreDataV2, 'pa_pos');
pa_distancegroup = createGroupData(PreDataV2, 'pa_distance');
pe_distancegroup = createGroupData(PreDataV2, 'pe_distance');
%
gazeTime = analyzeGazeTime(timesgroup, pa_distancegroup, pe_distancegroup);
gapAcpt = analyzeGapAcceptance(gapgroup, rbvgroup, pasposgroup);

%% Visualize data (needs reorganization, for now the calculations and visualization is done in the same script/function) 
gazeTimePlotter(gazeTime); 
visualizeGapAcceptance(gapAcpt);

