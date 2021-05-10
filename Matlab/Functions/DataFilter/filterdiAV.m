%% Filter trials in which the distraction vehicle did not drive 
% Author: Johnson Mok

function correct = filterdiAV(data, ED)
EDnr = split(ED,'_');
str_diAV = [2,6,10];
diAV_present = find(str_diAV==str2double(EDnr{3}));   % Check which condition is applied

if diAV_present == 1
    pass = find(data.diAV.pos.z > data.pe.pos.z(1),1,'first');
    if ~isempty(pass)
        correct = true;
    elseif isempty(pass)
        correct = false;
    end
else
    correct = true;
end
end



