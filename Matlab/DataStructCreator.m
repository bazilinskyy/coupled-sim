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

%% Get all csv data
Folder   = cd;
FileList = dir(fullfile(Folder, '**', '*.csv'));

% Convert csv file to data structure
for i=1:length(FileList)
    disp(['converting files (',num2str(i),'/',num2str(length(FileList)),')']);
    path        = FileList(i).folder;
    name        = FileList(i).name;
    filename    = join([path,'\',name]);
    Data        = CSVtoStruct(filename);

    %% Add to DataList
    ED_name = join(['Data_ED_',num2str(Data.expdefNr)]);
    participant = join(['participant_',num2str(Data.participantNr)]);
    trial = join(['trial_',num2str(Data.trialNr)]);
    ParentData.(ED_name).(Data.timescale).(participant).(trial) = Data;
end

%% Folder with data saved
disp('Saving data...');
save('AllData.mat','ParentData');
disp('... finished saving data.');