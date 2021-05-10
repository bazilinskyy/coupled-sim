%% Check_diAV
% This script returns a bool to indicate whether a distraction vehicle is 
% present based on the experiment definition number
% Author: Johnson Mok

function extracar = check_diAV(filename)
% The presence of a distraction vehicle is hardcoded in unity.
% The following experiment definitions contain a distraction vehicle:
% 2, 3, 6, 7, 10, and 11
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