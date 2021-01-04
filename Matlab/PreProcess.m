%% Pre-processing
% This script removes faulty data.
% Author: Johnson Mok
% Last Updated: 18-12-2020

function PostData = PreProcess(AllData)
disp('Start removing faulty data from gap acceptance.');
%% Gap acceptance
fields_ED = fieldnames(AllData);
for j = 1:length(fields_ED)
    fields_time = fieldnames(AllData.(fields_ED{j}));
    for k = 1:length(fields_time)
        fields_participants = fieldnames(AllData.(fields_ED{j}).(fields_time{k}));
        for idx = 1:length(fields_participants)
            fields_trials = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}));
            for i = 1:length(fields_trials)
                % Filter gapAcceptance
                AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.gapAcceptance(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.gapAcceptance<0) = 0;
            
                % Set startpoint
                % Function
            end
        end
    end
end
PostData = AllData;
disp('Finished removing faulty data from gap acceptance.');
end
