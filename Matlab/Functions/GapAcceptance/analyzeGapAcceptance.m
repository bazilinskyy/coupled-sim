%% Analyze gap acceptance
% This script ...
% Author: Johnson Mok
% Last Updated: 04-02-2021

% Input
%

% Output
%

function out = analyzeGapAcceptance(data,v,pasz,phase)
gapac  = getOrganizedDY(data);
AVvel  = getOrganizedDY(v);
AVposz = getOrganizedDY(pasz);
phaseorg = getOrganizedDY(phase);

% Gap Acceptance in phases
out.phases = getAllPhases(gapac, phaseorg);

%% Summing and smoothing gap acceptance values per variable combination
out.sumGap = calcAllSumGapAcceptance(gapac);

smoothfactor        = 11; % default 5
out.smoothgap = calcAllSmoothData(out.sumGap,smoothfactor);

% mean sum AV velocity
out.sumAVvel = calcAllMeanGroup(AVvel);
out.sumAVvel = calcAllAbsStruct(out.sumAVvel);

% mean sum AV Posz
out.sumAVposz = calcAllMeanGroup(AVposz);

% Decision certainty
% [DC_ND_Y, DC_ND_NY, DC_D_Y, DC_D_NY, totalMeanChange] = calcDecisionCertainty(gapac.ND_Y, gapac.ND_NY, gapac.D_Y, gapac.D_NY);
end

%% Helper functions
function out = getOrganizedDY(data)
fld = fieldnames(data.Data_ED_0.HostFixedTimeLog);
for i=1:length(fld)
    % ND_Y: ED 0, 4, 8
    ND_Y.map0.(fld{i}) = data.Data_ED_0.HostFixedTimeLog.(fld{i});
    ND_Y.map1.(fld{i}) = data.Data_ED_4.HostFixedTimeLog.(fld{i});
    ND_Y.map2.(fld{i}) = data.Data_ED_8.HostFixedTimeLog.(fld{i});
    % ND_NY: ED 1, 5, 9
    ND_NY.map0.(fld{i}) = data.Data_ED_1.HostFixedTimeLog.(fld{i});
    ND_NY.map1.(fld{i}) = data.Data_ED_5.HostFixedTimeLog.(fld{i});
    ND_NY.map2.(fld{i}) = data.Data_ED_9.HostFixedTimeLog.(fld{i});
    % D_Y: ED 2, 6, 10
    D_Y.map0.(fld{i}) = data.Data_ED_2.HostFixedTimeLog.(fld{i});
    D_Y.map1.(fld{i}) = data.Data_ED_6.HostFixedTimeLog.(fld{i});
    D_Y.map2.(fld{i}) = data.Data_ED_10.HostFixedTimeLog.(fld{i});
    % D_NY: ED 1, 5, 9
    D_NY.map0.(fld{i}) = data.Data_ED_3.HostFixedTimeLog.(fld{i});
    D_NY.map1.(fld{i}) = data.Data_ED_7.HostFixedTimeLog.(fld{i});
    D_NY.map2.(fld{i}) = data.Data_ED_11.HostFixedTimeLog.(fld{i});
end
out.ND_Y = ND_Y;
out.ND_NY = ND_NY;
out.D_Y = D_Y;
out.D_NY = D_NY;
end      

function out = getPhases(data, phase)
% Create out struct
phasestr = {'phase1', 'phase2', 'phase3', 'phase4', 'phase5'};
mapstr = {'map0','map1','map2'};
out = [];
for m=1:length(mapstr)
    for p=1:length(phasestr)
        if (~isfield(out, phasestr{p}))
            out.(mapstr{m}).(phasestr{p}) = [];
        end
    end
end
% Categorize gapacceptance by mapping and phase
fld_map = fieldnames(data);
for i=1:length(fld_map)
    fld_phase = fieldnames(out.(fld_map{i}));
    for j=1:length(data.(fld_map{i}).gapAcceptance)
        indgap = data.(fld_map{i}).gapAcceptance{j};
        idx = phase.(fld_map{i}).idx{j};
        for k=1:length(idx)
            out.(fld_map{i}).(fld_phase{k}){j} = indgap(idx(1,i):idx(2,i));
        end
    end
end
end
function out = getAllPhases(data, phase)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    out.(fld_con{c}) = getPhases(data.(fld_con{c}), phase.(fld_con{c}));
end
end

function out = calcSumGapAcceptance(data)
fld = fieldnames(data);
% Predefine array to largest size
max_size = 0;
for i = 1:length(fld)
    fld2 = fieldnames(data.(fld{i}));
    for j=1:length(fld2)
        [max_temp, ~] = max(cellfun('size', data.(fld{i}).(fld2{j}), 1));
        if max_temp>max_size
            max_size = max_temp;
        end
    end
end
x = ceil((max(max_size))/50)*50;
% Fill in the output array 
for j=1:length(fld)
    for k=1:length(fld2)
        out.(fld{j}).(fld2{k}) = zeros(x,1); 
        for i=1:length(data.(fld{j}).(fld2{k}))
            temp = data.(fld{j}).(fld2{k}){i};
            out.(fld{j}).(fld2{k})(1:length(temp)) = out.(fld{j}).(fld2{k})(1:length(temp)) + temp;
        end
        % Calculate as percentage
        out.(fld{j}).(fld2{k}) = out.(fld{j}).(fld2{k})*100/length(data.(fld{j}).(fld2{k}));
    end
end
end
function out = calcAllSumGapAcceptance(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    out.(fld_con{c}) = calcSumGapAcceptance(data.(fld_con{c}));
end
end

function out = smoothData(data,factor) 
% factor = Number of data points for calculating the smoothed value|Default = 5
fld_map = fieldnames(data);
for m = 1:length(fld_map)
    fld = fieldnames(data.(fld_map{m}));
    for i=1:length(fld)
        out.(fld_map{m}).(fld{i}) = smooth(data.(fld_map{m}).(fld{i}),factor);
    end
end
end
function out = calcAllSmoothData(data,factor)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    out.(fld_con{c}) = smoothData(data.(fld_con{c}),factor);
end
end

function out = calcMeanGroup(data)
% fill up the smaller arrays with the last known value such that all
% arrrays have the same length.
fld_map = fieldnames(data);
max_size = 0;
for m = 1:length(fld_map)
    fld = fieldnames(data.(fld_map{m}));
    for i=1:length(fld)
    [max_temp, ~] = max(cellfun('size', data.(fld_map{m}).(fld{i}), 1));
    if max_temp>max_size
        max_size = max_temp;
    end
    end
end
% Calc mean percentage
x = ceil((max(max_size))/50)*50;
for j=1:length(fld_map)
    for k=1:length(fld)
        out.(fld_map{j}).(fld{k}) = zeros(x,1); 
        for m=1:length(data.(fld_map{j}).(fld{k}))
            temp = data.(fld_map{j}).(fld{k}){m};
            out.(fld_map{j}).(fld{k})(1:length(temp)) = out.(fld_map{j}).(fld{k})(1:length(temp)) + temp;
            for di = length(temp)+1:x
                out.(fld_map{j}).(fld{k})(di) = out.(fld_map{j}).(fld{k})(di) + temp(end);
            end
        end
        out.(fld_map{j}).(fld{k}) = out.(fld_map{j}).(fld{k})/length(data.(fld_map{j}).(fld{k}));
    end
end
end
function out = calcAllMeanGroup(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    out.(fld_con{c}) = calcMeanGroup(data.(fld_con{c}));
end
end

function out = absStruct(data)
fld_map = fieldnames(data);
for m=1:length(fld_map)
    fld = fieldnames(data.(fld_map{m}));
    for i=1:length(fld)
        out.(fld_map{m}).(fld{i}) = abs(data.(fld_map{m}).(fld{i}));
    end
end
end
function out = calcAllAbsStruct(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    out.(fld_con{c}) = absStruct(data.(fld_con{c}));
end
end

function [DC_ND_Y, DC_ND_NY, DC_D_Y, DC_D_NY, totalMeanChange] = calcDecisionCertainty(ND_Y, ND_NY, D_Y, D_NY)
Input = {ND_Y, ND_NY, D_Y, D_NY};
out = zeros(1,3);
for i = 1:length(Input)
    data = Input{i};
    changes = zeros(size(data));
    for col = 1:size(data,2)
        for row = 1:size(data,1)
            changes(row, col) = sum(diff(data{row,col})~=0);
        end
    end
    out(i,:) = mean(changes);
end
DC_ND_Y  = out(1,:);
DC_ND_NY = out(2,:);
DC_D_Y   = out(3,:);
DC_D_NY  = out(4,:);
totalMeanChange = mean(out);
end
