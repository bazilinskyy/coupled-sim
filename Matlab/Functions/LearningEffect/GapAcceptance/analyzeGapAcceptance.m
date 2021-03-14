%% Analyze gap acceptance
% This script analyzes the 'safe to cross' button press.
% Author: Johnson Mok
% Last Updated: 04-02-2021

% Input
%

% Output
% Crossing performance metric
% Decision certainty

function out = analyzeGapAcceptance(data,v,pasz,phase)
gapac  = getOrganizedDY(data);
AVvel  = getOrganizedDY(v);
AVposz = getOrganizedDY(pasz);
phaseorg = getOrganizedDY(phase);

% Gap Acceptance in phases
out.phases = getAllPhases(gapac, phaseorg); % Individual
out.phasesSum = calcAllSumPhases(out.phases);
out.phasesPer = calcAllSumPercentage(out.phasesSum);

%% Decision certainty
% out.DC = calcDecisionCertainty(out.phases, 'ND_Y', 'map0');
out.DC = calcAllDecisionCertainty(out.phases);

% Statistical analysis
SPSS = getDecisionCertaintySPSSMatrix(out.DC);
%     writematrix(SPSS.ND_Y,'SPSS_DecisionCertainty_ND_Y.csv'); 
%     writematrix(SPSS.ND_NY,'SPSS_DecisionCertainty_ND_NY.csv'); 
%     writematrix(SPSS.D_Y,'SPSS_DecisionCertainty_D_Y.csv'); 
%     writematrix(SPSS.D_NY,'SPSS_DecisionCertainty_D_NY.csv'); 
    
SPSS_ANOVA = getDecisionCertaintySPSS_ANOVAMatrix(out.DC);
%     writematrix(SPSS_ANOVA.yield,'SPSS_DCANOVA_Y.csv'); 
%     writematrix(SPSS_ANOVA.noyield,'SPSS_DCANOVA_NY.csv'); 


%% Summing and smoothing gap acceptance values per variable combination
out.sumGap = calcAllSumGapAcceptance(gapac);

smoothfactor        = 11; % default 5
out.smoothgap = calcAllSmoothData(out.sumGap,smoothfactor);

% mean sum AV velocity
out.sumAVvel = calcAllMeanGroup(AVvel);
out.sumAVvel = calcAllAbsStruct(out.sumAVvel);

% mean sum AV Posz
out.sumAVposz = calcAllMeanGroup(AVposz);

end

%% Helper functions - phases
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
fld_map = fieldnames(data);
for m=1:length(fld_map)
    for p=1:size(phase.(fld_map{m}).idx{1},2)
        out.(mapstr{m}).(phasestr{p}) = [];
    end
end
% Categorize gapacceptance by mapping and phase
for m=1:length(fld_map)
    fld_phase = fieldnames(out.(fld_map{m}));
    for i=1:length(data.(fld_map{m}).gapAcceptance)
        indgap = data.(fld_map{m}).gapAcceptance{i};
        idx = phase.(fld_map{m}).idx{i};
        for k=1:size(idx,2)
            out.(fld_map{m}).(fld_phase{k}){i} = indgap(idx(1,k):idx(2,k));
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

function out = calcSumPhases(data, con, mapping)
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}));
m = find(strcmp(fld_map,mapping));
fld_phase = fieldnames(data.(fld_con{c}).(fld_map{m}));
% Find largest array size (per phase)
max_size = zeros(5,1);
for p=1:length(fld_phase)
	[max_size(p), ~] = max(cellfun('size', data.(fld_con{c}).(fld_map{m}).(fld_phase{p}), 1));
end
% Fill in the output array 
for p=1:length(fld_phase)
%     out.(fld_con{c}).(fld_map{m}).(fld_phase{p}) = zeros(max_size(p),1);
    out.(fld_phase{p}) = zeros(max_size(p),1);
    for i=1:length(data.(fld_con{c}).(fld_map{m}).(fld_phase{p}))
        temp = data.(fld_con{c}).(fld_map{m}).(fld_phase{p}){i};
%         out.(fld_con{c}).(fld_map{m}).(fld_phase{p})(1:length(temp)) = out.(fld_con{c}).(fld_map{m}).(fld_phase{p})(1:length(temp)) + temp;
        out.(fld_phase{p})(1:length(temp)) = out.(fld_phase{p})(1:length(temp)) + temp;
        if(length(temp)<max_size(p))
            for e = length(temp)+1:max_size(p) 
                out.(fld_phase{p})(e) = out.(fld_phase{p})(e) + temp(end);
            end
        end
    end
%     out.(fld_con{c}).(fld_map{m}).(fld_phase{p}) = out.(fld_con{c}).(fld_map{m}).(fld_phase{p})*100/length(data.(fld_con{c}).(fld_map{m}).(fld_phase{p}));
    out.(fld_phase{p}) = out.(fld_phase{p})*100/length(data.(fld_con{c}).(fld_map{m}).(fld_phase{p}));
end
end

function out = calcAllSumPhases(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        temp = calcSumPhases(data, (fld_con{c}), (fld_map{m}));
        out.(fld_con{c}).(fld_map{m}) = temp;
    end
end
end

function out = calcAllSumPercentage(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        fld_phase = fieldnames(data.(fld_con{c}).(fld_map{m}));
        for p=1:length(fld_phase)
        temp = data.(fld_con{c}).(fld_map{m}).(fld_phase{p});
        out.(fld_con{c}).(fld_map{m})(p) = sum(temp)/length(temp);
        end
    end
end
end

%% Helper functions- DC
function out = calcDecisionCertainty(data, con, mapping)
% Calculate number reversal per trial, sum up number reversal of per phase
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}));
m = find(strcmp(fld_map,mapping));
fld_phase = fieldnames(data.(fld_con{c}).(fld_map{m}));

% Calc reversal rate
dat = data.(fld_con{c}).(fld_map{m});
for p = 1:length(fld_phase)
    for i =1:length(dat.(fld_phase{p}))
        temp.(fld_con{c}).(fld_map{m}).(fld_phase{p}){i} = sum(abs(diff(dat.(fld_phase{p}){i})));
    end
end

% Sum up for all phases
if length(fld_phase) == 3
    for i =1:length(dat.(fld_phase{1}))
	temp2.(fld_con{c}).(fld_map{m})(i) = temp.(fld_con{c}).(fld_map{m}).(fld_phase{1}){i} + temp.(fld_con{c}).(fld_map{m}).(fld_phase{2}){i} +...
	temp.(fld_con{c}).(fld_map{m}).(fld_phase{3}){i};
    end
elseif length(fld_phase) == 1
    for i =1:length(dat.(fld_phase{1}))
	temp2.(fld_con{c}).(fld_map{m})(i) = temp.(fld_con{c}).(fld_map{m}).(fld_phase{1}){i};
    end 
end

% Mean of reversal rates
out.rate = temp2.(fld_con{c}).(fld_map{m});
out.mean = mean(temp2.(fld_con{c}).(fld_map{m}));
out.std = std(temp2.(fld_con{c}).(fld_map{m}));
end
function out = calcAllDecisionCertainty(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        out.(fld_con{c}).(fld_map{m}) = calcDecisionCertainty(data, fld_con{c}, fld_map{m});
    end
end
end

function out = getDecisionCertaintySPSSMatrix(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    maxsize = 0;
    for m=1:length(fld_map)
        if length(data.(fld_con{c}).(fld_map{m}).rate)>maxsize
            maxsize = length(data.(fld_con{c}).(fld_map{m}).rate);
        end
    end
    out.(fld_con{c}) = nan(maxsize,3);
    for m=1:length(fld_map)
        out.(fld_con{c})(1:length(data.(fld_con{c}).(fld_map{m}).rate),m) = data.(fld_con{c}).(fld_map{m}).rate;
    end
end
end

function [yield, distraction] = getDistractionYielding(con)
if(strcmp(con,'ND_Y'))
    yield = 1;
    distraction = 0;
elseif(strcmp(con,'D_Y'))
    yield = 1;
    distraction = 1;
elseif(strcmp(con,'D_NY'))
    yield = 2;
    distraction = 1;
elseif(strcmp(con,'ND_NY'))
    yield = 2;
    distraction = 0;
end
end
function out = getDecisionCertaintySPSS_ANOVAMatrix(data)
% output [mapping, distraction, decision certainty]
fld_con = fieldnames(data);
out.yield = [];
out.noyield = [];
fld_yield = fieldnames(out);
for c=1:length(fld_con)
    [yld, dist] = getDistractionYielding(fld_con{c});
    fld_map = fieldnames(data.(fld_con{c}));
    temp = [];
    map = [];
    distr = [];
    for m=1:length(fld_map)
        temp = [temp, data.(fld_con{c}).(fld_map{m}).rate];
        map = [map, (m-1)*ones(size(data.(fld_con{c}).(fld_map{m}).rate))];
        distr = [distr, dist*ones(size(data.(fld_con{c}).(fld_map{m}).rate))];
    end
    tempdata = [map', distr', temp'];
    out.(fld_yield{yld}) = [out.(fld_yield{yld}); tempdata];
end
end
%% - old
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

