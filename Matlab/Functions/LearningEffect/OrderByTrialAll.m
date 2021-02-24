%% Order by trial all
% Hierarchy: OrderByTrialAll -> OrderByTrial
% Author: Johnson Mok
% Last Updated: 24-02-2021

function out = OrderByTrialAll(data)
% Fill array
fld_ED = fieldnames(data);
for ed=1:length(fld_ED)
    fld_parti = fieldnames(data.(fld_ED{ed}).HostFixedTimeLog);
    for parti=1:length(fld_parti)
        temp = OrderByTrial(data,fld_ED{ed},fld_parti{parti});
        fld_trial = fieldnames(temp);
        for tr=1:length(fld_trial)
            out.(fld_ED{ed}).(fld_trial{tr}){parti} = temp.(fld_trial{tr});
        end
    end
end
% Remove empty cells
for ed=1:length(fld_ED)
	for tr=1:length(fld_trial)
        out.(fld_ED{ed}).(fld_trial{tr}) = out.(fld_ED{ed}).(fld_trial{tr})(~cellfun('isempty',out.(fld_ED{ed}).(fld_trial{tr})));
	end
end
end

