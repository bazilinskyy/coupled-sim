%% Run Me
% This is the main script to perform analysis on the data.
% Author: Johnson Mok
% Last Updated: 18-12-2020
clc;
clear;
close all;

%% Load Data 
if(~exist('AllData.mat'))
% Notice: depending on the number and size of the logfiles, it will take multiple hours to create the datastructure. 
    run('FileOrdener.m');       % Orden all files from log data to the file structure.
    run('DataStructCreator.m'); % Convert CSV files to matlab data structure and save it.
else
    load('AllData.mat');        % Load pre-converted data
end

%% Pre-Process
PreData = PreProcess(ParentData);

%% What do we want to do with the data?
% execute calculation functions here

%% Visualize data 
% execute plot data functions here
%     figure
%     plot(PreData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14.pe.gapAcceptance);
dataPlotter_V2(PreData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14);
sum(PreData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14.pa.distance<0)
length(PreData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14.Time)