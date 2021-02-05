%% Filter wrongly executed trials 
% This script checks whether the participants followed the instructions
% correctly.
% Hierarchy: FilterWrongExecutions -> filterWronglyExecuted
% Author: Johnson Mok
% Last Updated: 05-02-2021

function correct = filterWronglyExecuted(data, ED)
% Define (non)yielding experiment definition numbers
strY = {'0','2','4','6','8','10'};
strNY = {'1','3','5','7','9','11'};
instrY = contains(ED,strY);     % Check which condition is applied
instrNY = contains(ED,strNY);   % Check which condition is applied
idx_yield = find(round(data.pa.world.rb_v.z,1)==0,1,'first');   % Check for yielding
% Determine if the passenger acted as instructed
if(~isempty(idx_yield) && instrY)
    correct = true;
elseif(isempty(idx_yield) && instrNY)
    correct = true;
else % Remove wrongly executed trials
    correct = false;
end
end

