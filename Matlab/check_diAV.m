%% This script returns a bool to indicate whether a distraction vehicle is present based on the experiment definition number
% Author: Johnson Mok
% Last updated: 17-12-2020

% The presence of a distraction vehicle is hardcoded in unity.
% The following experiment definitions contain a distraction vehicle:
% 2, 3, 6, 7, 10, and 11
function extracar = check_diAV(filename)
extracar    = false;
name_split  = split(filename,'_');
idx         = find(contains(name_split,'expdef'));
expdef_nr   = str2double(name_split{idx+1});
diAV_nr     = [2,3,6,7,10,11];
for i = 1:length(diAV_nr)
     if(expdef_nr == diAV_nr(i))
         extracar = true;
     end
end
end