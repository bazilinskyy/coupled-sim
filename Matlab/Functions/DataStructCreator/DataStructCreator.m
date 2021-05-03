%% This script creates one big data structure containing all the data from the logfiles
% Ordened as follows:
% - Experiment definition
% - Timescale (Host fixed time/time)
% - Participant
% - Trial

% Author: Johnson Mok
% Last updated: 17-12-2020
clear
clc
close all

disp('Creating data structure...');
%% Get all csv data
Folder   = join([cd,'\LOG_ordened']); %cd
FileList = dir(fullfile(Folder, '**', '*.csv'));

% Convert csv file to data structure
for i=1:length(FileList)
    disp(['converting files (',num2str(i),'/',num2str(length(FileList)),')']);
    path        = FileList(i).folder;
    name        = FileList(i).name;
    filename    = join([path,'\',name]);
    Data        = CSVtoStruct(filename);
%     Data        = CSVtoStruct_simple(filename);
    
    %% Add to DataList
    ED_name = join(['Data_ED_',num2str(Data.expdefNr)]);
    participant = join(['participant_',num2str(Data.participantNr)]);
    trial = join(['trial_',num2str(Data.trialNr)]);
    ParentData.(ED_name).(Data.timescale).(participant).(trial) = Data;
end

disp('Finished creating data structure.');

%% Folder with data saved
disp('Saving data...');
save('AllData.mat','ParentData', '-v7.3');
disp('... finished saving data.');