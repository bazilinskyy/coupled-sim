%% Check Unity
clc
close all
clear

%% Add path to functions
addpath(genpath('C:\Users\DhrCS\Documents\GitHub\coupled-sim\Matlab\Functions'));

%% Get all csv data
% run('DataStructCreator_Unity.m'); % Convert CSV files to matlab data structure and save it.
% load('AllData_4in.mat'); % Result: standstill time = 1.56 seconds.
load('AllData6out.mat'); % Result: resume timer is 6.28 (trigger till restart). Standstill is 2.6 seconds.
%% Position
Data = ParentData.Data_ED_0.HostFixedTimeLog.participant_0;
newData = setStartEndTime(Data);

%% Index
clc; close all
out = getAllindices(newData);

           




