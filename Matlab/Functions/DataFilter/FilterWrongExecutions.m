%% FilterWrongExecutions
% This script removes faulty the trials in which the participant did not
% follow the instructions
% Hierarchy: FilterWrongExecutions -> filterWronglyExecuted
% Author: Johnson Mok

function [PostData, WrongData] = FilterWrongExecutions(AllData)
disp('Start removing wrongly executed trials.');
fields_ED = fieldnames(AllData);
for j = 1:length(fields_ED)
    fields_time = fieldnames(AllData.(fields_ED{j}));
    for k = 1:length(fields_time)
        fields_participants = fieldnames(AllData.(fields_ED{j}).(fields_time{k}));
        for idx = 1:length(fields_participants)
            fields_trials = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}));
            for i = 1:length(fields_trials)
                % Filter wrongly executed trials
                correct = filterWronglyExecuted(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}), fields_ED{j});
                correct2 = filterLATY(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}), fields_ED{j});
                correct_diAV = filterdiAV(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}), fields_ED{j});
                if(correct == false || correct2 == false || correct_diAV == false)
                    if correct2 == false
                        disp(['Wrongly executed correct LATY: ', (fields_ED{j}),'_',(fields_time{k}),'_',(fields_participants{idx}),'_',(fields_trials{i})]);
                    elseif correct_diAV == false
                        disp(['DiAV error: ', (fields_ED{j}),'_',(fields_time{k}),'_',(fields_participants{idx}),'_',(fields_trials{i})]);     
                    end
                    disp(['Wrongly executed GTY: ', (fields_ED{j}),'_',(fields_time{k}),'_',(fields_participants{idx}),'_',(fields_trials{i})]);
                    WrongData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}) = AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i});
                    AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}) = rmfield(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}),(fields_trials{i}));
                end
            end
        end
    end
end
PostData = AllData;
disp('Finished removing wrongly exected trials.');
end
