%% Filter wrongly executed trials 
% This script checks whether the participants followed the instructions
% correctly in the GTY mapping.
% Hierarchy: FilterWrongExecutions -> filterWronglyExecuted
% Author: Johnson Mok

function correct = filterWronglyExecuted(data, ED)
% Define (non)yielding experiment definition numbers
EDnr = split(ED,'_');
strY = 0:2:10;
strNY = 1:2:11;
instrY = find(strY==str2double(EDnr{3}));   % Check which condition is applied
instrNY = find(strNY==str2double(EDnr{3})); % Check which condition is applied
% Check for yielding with AV velocity data
idx_yield = find(round(data.pa.world.rb_v.z,1)==0,1,'first');   
% Check for yielding with passenger gaze data
gazeInRange = ismember(round(data.pa.distance,1), 14.4:0.1:25);
gazeYield = length(find(gazeInRange==1));
% Determine if the passenger acted as instructed
if((~isempty(idx_yield) || gazeYield>4) && ~isempty(instrY))
    correct = true;
elseif((isempty(idx_yield) || isempty(gazeYield)) && ~isempty(instrNY))
    correct = true;
else % Remove wrongly executed trials
    correct = false;
end
end

