%% Analyze gap acceptance
% This script analyzes the 'safe to cross' button press.
% Author: Johnson Mok

% Output
% Crossing performance metric
% Decision certainty
% Statistical analysis of Decision Certainty

function out = analyzeGapAcceptance(data,v,pasz,phase,order)
gapac  = getOrganizedDY(data);
AVvel  = getOrganizedDY(v);
AVposz = getOrganizedDY(pasz);
phaseorg = getOrganizedDY(phase);

% Gap Acceptance in phases
out.phases    = getAllPhases(gapac, phaseorg); % Individual
out.phasesSum = calcAllSumPhases(out.phases);
out.phasesPer = calcAllSumPercentage(out.phasesSum);

%% Decision certainty
out.DC = calcAllDecisionCertainty(out.phases); 

% ======== Statistical analysis ========
out.DCpp = DCPerParticipant(out.DC, order);
out.DC_STD = calcAllDCSTD(out.DCpp);
out.SPSS = getDecisionCertaintySPSSMatrix(out.DCpp);
t_D_NY = pairedSamplesttest(out.SPSS.D_NY);
t_D_Y = pairedSamplesttest(out.SPSS.D_Y);
t_ND_NY = pairedSamplesttest(out.SPSS.ND_NY);
t_ND_Y = pairedSamplesttest(out.SPSS.ND_Y);

D_D_NY = CohensD(out.SPSS.D_NY);
D_D_Y = CohensD(out.SPSS.D_Y);
D_ND_NY = CohensD(out.SPSS.ND_NY);
D_ND_Y = CohensD(out.SPSS.ND_Y);

% Table
out.StatisticalAnalysis_DC_D_NY = getTableTtest(t_D_NY);
out.StatisticalAnalysis_DC_D_Y = getTableTtest(t_D_Y);
out.StatisticalAnalysis_DC_ND_NY = getTableTtest(t_ND_NY);
out.StatisticalAnalysis_DC_ND_Y = getTableTtest(t_ND_Y);

out.StatisticalAnalysis_DC_Cohen = getTableCohen(D_D_NY, D_D_Y, D_ND_NY, D_ND_Y);

%% Summing and smoothing gap acceptance values per variable combination
out.sumGap = calcAllSumGapAcceptance(gapac);

smoothfactor  = 11; % default 5
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
            if(isempty(out.(fld_map{m}).(fld_phase{k}){i})) 
                idx
            end
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
                if(isempty(temp))
                    temp = 0;
                end
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
%         wholeTrial{i} = [dat.(fld_phase{1}){i}; dat.(fld_phase{2}){i}; dat.(fld_phase{3}){i}];
        temp.(fld_con{c}).(fld_map{m}).(fld_phase{p}){i} = sum(abs(diff(dat.(fld_phase{p}){i})));
    end
end
% for i = 1:length(wholeTrial)
%     temp3.(fld_con{c}).(fld_map{m})(i) = sum(abs(diff(wholeTrial{i})));
% end

% Sum up for all phases
if length(fld_phase) == 4
    for i =1:length(dat.(fld_phase{1}))
	temp2.(fld_con{c}).(fld_map{m})(i) = temp.(fld_con{c}).(fld_map{m}).(fld_phase{1}){i} + temp.(fld_con{c}).(fld_map{m}).(fld_phase{2}){i} +...
	temp.(fld_con{c}).(fld_map{m}).(fld_phase{3}){i} + temp.(fld_con{c}).(fld_map{m}).(fld_phase{4}){i} ;
    end
elseif length(fld_phase) == 3
    for i =1:length(dat.(fld_phase{1}))
	temp2.(fld_con{c}).(fld_map{m})(i) = temp.(fld_con{c}).(fld_map{m}).(fld_phase{1}){i} + temp.(fld_con{c}).(fld_map{m}).(fld_phase{2}){i} +...
	temp.(fld_con{c}).(fld_map{m}).(fld_phase{3}){i};
    end
elseif length(fld_phase) == 2
    for i =1:length(dat.(fld_phase{1}))
	temp2.(fld_con{c}).(fld_map{m})(i) = temp.(fld_con{c}).(fld_map{m}).(fld_phase{1}){i}+ + temp.(fld_con{c}).(fld_map{m}).(fld_phase{2}){i};
    end 
end

% Mean of reversal rates
out.rate = temp2.(fld_con{c}).(fld_map{m});
out.mean = mean(temp2.(fld_con{c}).(fld_map{m}));
out.std = std(temp2.(fld_con{c}).(fld_map{m}));

%% debug
% figure; 
% plot(temp2.ND_Y.map0); 
% hold on; 
% plot(temp3.ND_Y.map0);

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

function out = DCPerParticipant(data, order)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        A = data.(fld_con{c}).(fld_map{m}).rate;
        B = order.(fld_con{c}).(fld_map{m}).Pnr;
        for i=1:max(B)
            out.(fld_con{c}).(fld_map{m})(i) = mean(A(find(B==i)));
        end
    end
end
end
function out = getDecisionCertaintySPSSMatrix(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    maxsize = 0;
    for m=1:length(fld_map)
        if length(data.(fld_con{c}).(fld_map{m}))>maxsize
            maxsize = length(data.(fld_con{c}).(fld_map{m}));
        end
    end
    out.(fld_con{c}) = nan(maxsize,3);
    for m=1:length(fld_map)
        out.(fld_con{c})(1:length(data.(fld_con{c}).(fld_map{m})),m) = data.(fld_con{c}).(fld_map{m});
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

function out = calcDCSTD(data, con, mapping)
% Calculate number reversal per trial, sum up number reversal of per phase
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}));
m = find(strcmp(fld_map,mapping));

% std
out = std(data.(fld_con{c}).(fld_map{m}));
end
function out = calcAllDCSTD(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        out.(fld_con{c}).(fld_map{m}) = calcDCSTD(data, fld_con{c}, fld_map{m});
    end
end
end
%% Statistical analysis functions
function out = pairedSamplesttest(data)
[~,p1,~,stats1] = ttest(data(:,1), data(:,2));
[~,p2,~,stats2] = ttest(data(:,2), data(:,3));
[~,p3,~,stats3] = ttest(data(:,1), data(:,3));
out = zeros(3,3);
out(1,:) = [stats1.tstat, stats1.df, p1];
out(2,:) = [stats2.tstat, stats2.df, p2];
out(3,:) = [stats3.tstat, stats3.df, p3];
end
function out = CohensD(data)
pair12 = data(:,1)-data(:,2); % baseline - mapping 1
pair23 = data(:,2)-data(:,3); % mapping 1 - mapping 2
pair13 = data(:,1)-data(:,3); % baseline - mapping 2

out = zeros(3,3);
out(1,:) = calcCohen(pair12);
out(2,:) = calcCohen(pair23);
out(3,:) = calcCohen(pair13);
end
function out = calcCohen(data)
m = mean(data);
s = std(data);
D = m/s;
out = [m, s, D];
end
function T = getTableTtest(data)
% Get data
GTY_base = ['t(',num2str(data(1,2)),') = ',num2str(data(1,1)),' p = ',num2str(num2str(data(1,3)))];
LATY_base = ['t(',num2str(data(3,2)),') = ',num2str(data(3,1)),' p = ',num2str(num2str(data(3,3)))];
GTY_LATY = ['t(',num2str(data(2,2)),') = ',num2str(data(2,1)),' p = ',num2str(num2str(data(2,3)))];
% Create column data
Mapping = {'Baseline';'GTY';'LATY'};
Baseline = {'X';GTY_base;LATY_base};
GTY = {'X';'X';GTY_LATY};
LATY = {'X';'X';'X'};
% Create Table
T = table(Mapping, Baseline, GTY, LATY);
end
function T = getTableCohen(D_D_NY, D_D_Y, D_ND_NY, D_ND_Y)
% Create column data
Mapping = {'Baseline - GTY';'GTY - LATY';'Baseline - LATY'};
D_NY = D_D_NY(:,3);
D_Y = D_D_Y(:,3);
ND_NY = D_ND_NY(:,3);
ND_Y = D_ND_Y(:,3);
% Create Table
T = table(Mapping, D_NY, D_Y, ND_NY, ND_Y);
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

