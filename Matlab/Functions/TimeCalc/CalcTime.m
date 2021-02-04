%% CalcTime
% This script calculates all eye-tracking times
% Hierarchy: CalcTime -> getTime -> calcGazeTime
% Author: Johnson Mok
% Last Updated: 18-01-2021

function T = CalcTime(AllData,AllData_unp)
disp('Start calculating eye-tracking times.');
fields_ED = fieldnames(AllData);
for j = 1:length(fields_ED)
    fields_time = fieldnames(AllData.(fields_ED{j}));
    for k = 1:length(fields_time)
        fields_participants = fieldnames(AllData.(fields_ED{j}).(fields_time{k}));
        for idx = 1:length(fields_participants)
            fields_trials = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}));
            for i = 1:length(fields_trials)
                % Perform task here
                T.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}) = getTime(...
                    AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}),...
                    AllData_unp.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}));
            end
        end
    end
end
disp('Finished calculating eye-tracking times.');
end
