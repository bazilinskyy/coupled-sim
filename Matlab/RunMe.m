%% Run Me
% This is the main script to perform analysis on the data.
% Author: Johnson Mok
% Last Updated: 03-01-2020
clc;
clear;
close all;

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
    run('SetStartAndEnd.m');
end
if(exist('PreDataV2.mat'))
    load('PreDataV2.mat');
end

%% What do we want to do with the data?
% execute calculation functions here

%% Visualize data 
% execute plot data functions here
% dataPlotter_V2(PreData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14,'pedestrian');
% PlotAll(PreData);

% SetStartAndEnd(PreData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14);
% disp(size(PreData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14.pa.distance));
    % gap acceptance vs time
    figure;
    grid on;
    hold on;
    plot(PreData.Data_ED_0.HostFixedTimeLog.participant_1.trial_9.Time, PreData.Data_ED_0.HostFixedTimeLog.participant_1.trial_9.pe.gapAcceptance, 'r');
    plot(PreDataV2.Data_ED_0.HostFixedTimeLog.participant_1.trial_9.Time, PreDataV2.Data_ED_0.HostFixedTimeLog.participant_1.trial_9.pe.gapAcceptance, 'g');
    ylabel('Gap acceptance');
    title('Gap acceptance vs Time');
