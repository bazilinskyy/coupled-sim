%% No Category Post Block Calculations
% Author: Johnson Mok
% Last Updated: 02-02-2021

% This script analyses the post block questions which do not fit into any
% predefined category (such as acceptance).

% Input: 
% 1) List of MISC scores as a cell array.

% Output:
% directLaser val, freq and names
% laserDistract val, freq and names
% Vehicle act predicted val, freq and names
% Mapping preference val, freq and names
% Clear when vehicle yields val, freq and names

function out = NoCategoryPostBlock(Data_block_pa, Data_block_pe)
%% Total number of experiments
out.N = length(Data_block_pa.Date)/3;

%% Calulations - Passenger
% Easy to direct the eye-gaze visualization
out.directLaser_pa.all = matchWithMapping(Data_block_pa.mapping, Data_block_pa.directLaser);
out.directLaser_pa.mean = mean(out.directLaser_pa.all);
out.directLaser_pa.std = std(out.directLaser_pa.all);
[out.directLaser_val_pa, out.directLaser_name_pa] = calcSevenLikertScaleMapping(out.directLaser_pa.all,'agree');
out.directLaser_val_pa = convPerc(out.directLaser_val_pa);

% Eye-gaze visualization distracting
out.laserDistract_pa.all = matchWithMapping(Data_block_pa.mapping, Data_block_pa.laserDistract);
out.laserDistract_pa.mean = mean(out.laserDistract_pa.all);
out.laserDistract_pa.std = std(out.laserDistract_pa.all);
[out.laserDistract_val_pa, out.laserDistract_name_pa] = calcSevenLikertScaleMapping(out.laserDistract_pa.all,'agree');
out.laserDistract_val_pa = convPerc(out.laserDistract_val_pa);

% Vehicle acted as predicted 
out.VAP_pa.all = matchWithMapping(Data_block_pa.mapping, Data_block_pa.vehicleActPredicted);
out.VAP_pa.mean = mean(out.VAP_pa.all);
out.VAP_pa.std = std(out.VAP_pa.all);
[out.VAP_val_pa, out.VAP_name_pa] = calcSevenLikertScaleMapping(out.VAP_pa.all,'easy');
out.VAP_val_pa = convPerc(out.VAP_val_pa);

%% Calulations - Pedestrian
% Mapping preference
out.prefMapping.all = matchWithMapping(Data_block_pa.mapping, Data_block_pe.prefMapping);
out.prefMapping.mean = mean(out.prefMapping.all);
out.prefMapping.std = std(out.prefMapping.all);
[out.prefMapping_val, out.prefMapping_name] = calcSevenLikertScaleMapping(out.prefMapping.all,'agree');
out.prefMapping_val = convPerc(out.prefMapping_val);

% Clear vehicle yielding 
out.clearVehicleYield.all = matchWithMapping(Data_block_pe.mapping, Data_block_pe.clearVehicleYield);
out.clearVehicleYield.mean = mean(out.clearVehicleYield.all);
out.clearVehicleYield.std = std(out.clearVehicleYield.all);
[out.clearVehicleYield_val, out.clearVehicleYield_name] = calcSevenLikertScaleMapping(out.clearVehicleYield.all,'agree');
out.clearVehicleYield_val = convPerc(out.clearVehicleYield_val);

end

%% Helper functions
function [outVal, name] = calcSevenLikertScaleMapping(data,string)
if(strcmp(string,'like'))
    name = {'Extremely unlikely';'Unlikely'; 'Bit unlikely'; 'Neutral';'bit likely';'Likely';'Extremely likely'};
elseif(strcmp(string,'agree'))
    name = {'Strongly disagree';'Disagree'; 'Slightly disagree'; 'Neutral';'Slightly agree';'Agree';'Strongly agree'};
elseif(strcmp(string,'easy'))
	name = {'Extremely difficult';'Difficult'; 'Slightly difficult'; 'Neutral';'Slightly easy';'Easy';'Extremely easy'};
end
outVal = zeros(size(data,2),length(name));
for j=1:size(data,2)
    [GC,GR] = groupcounts(data(:,j)); 
    val = zeros(1,length(name));
    for i=1:length(name)
        index = find(GR==i);
        if(~isempty(index))
            val(i) = GC(index);
        end
    end
    outVal(j,:) = val;
end
end
function output = matchWithMapping(dataMap, data)
Idx0 = contains(dataMap,'Baseline');
Idx1 = contains(dataMap,'Mapping 1 (stop when looking)');
Idx2 = contains(dataMap,'Mapping 2 (stop when NOT looking');
Data_baseline = data(Idx0,:);
Data_map1     = data(Idx1,:);
Data_map2     = data(Idx2,:);
output = [Data_baseline, Data_map1, Data_map2];
end
function out = convPerc(in)
out = 100*in/max(sum(in,2));
end