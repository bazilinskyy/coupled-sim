%% Analyze gap acceptance
% This script ...
% Author: Johnson Mok
% Last Updated: 04-02-2021

% Input
%

% Output
%

function out = analyzeGapAcceptance(data,v,pasz)
gapac  = getOrganizedDY(data);
AVvel  = getOrganizedDY(v);
AVposz = getOrganizedDY(pasz);

% Summing and smoothing gap acceptance values per variable combination
out.sumGap.ND_Y  = calcSumGapAcceptance(gapac.ND_Y);
out.sumGap.ND_NY = calcSumGapAcceptance(gapac.ND_NY);
out.sumGap.D_Y   = calcSumGapAcceptance(gapac.D_Y);
out.sumGap.D_NY  = calcSumGapAcceptance(gapac.D_NY);

smoothfactor        = 11; % default 5
out.smoothgap.ND_Y  = smoothData(out.sumGap.ND_Y,smoothfactor);
out.smoothgap.ND_NY = smoothData(out.sumGap.ND_NY,smoothfactor);
out.smoothgap.D_Y   = smoothData(out.sumGap.D_Y,smoothfactor);
out.smoothgap.D_NY  = smoothData(out.sumGap.D_NY,smoothfactor);

% mean sum AV velocity
out.sumAVvel.ND_Y = abs(calcMeanGroup(AVvel.ND_Y));
out.sumAVvel.ND_NY = abs(calcMeanGroup(AVvel.ND_NY));
out.sumAVvel.D_Y = abs(calcMeanGroup(AVvel.D_Y));
out.sumAVvel.D_NY = abs(calcMeanGroup(AVvel.D_NY));

% mean sum AV Posz
out.sumAVposz.ND_Y = calcMeanGroup(AVposz.ND_Y);
out.sumAVposz.ND_NY = calcMeanGroup(AVposz.ND_NY);
out.sumAVposz.D_Y = calcMeanGroup(AVposz.D_Y);
out.sumAVposz.D_NY = calcMeanGroup(AVposz.D_NY);

% Decision certainty
% [DC_ND_Y, DC_ND_NY, DC_D_Y, DC_D_NY, totalMeanChange] = calcDecisionCertainty(gapac.ND_Y, gapac.ND_NY, gapac.D_Y, gapac.D_NY);
end

%% Helper functions
function out = getOrganizedDY(data)
field = fieldnames(data.Data_ED_0.HostFixedTimeLog);
% ND_Y: ED 0, 4, 8
ND_Y(:,1) = data.Data_ED_0.HostFixedTimeLog.(field{:});
ND_Y(:,2) = data.Data_ED_4.HostFixedTimeLog.(field{:});
ND_Y(:,3) = data.Data_ED_8.HostFixedTimeLog.(field{:});
% ND_NY: ED 1, 5, 9
ND_NY(:,1) = data.Data_ED_1.HostFixedTimeLog.(field{:});
ND_NY(:,2) = data.Data_ED_5.HostFixedTimeLog.(field{:});
ND_NY(:,3) = data.Data_ED_9.HostFixedTimeLog.(field{:});
% D_Y: ED 2, 6, 10
D_Y(:,1) = data.Data_ED_2.HostFixedTimeLog.(field{:});
D_Y(:,2) = data.Data_ED_6.HostFixedTimeLog.(field{:});
D_Y(:,3) = data.Data_ED_10.HostFixedTimeLog.(field{:});
% D_NY: ED 1, 5, 9
D_NY(:,1) = data.Data_ED_3.HostFixedTimeLog.(field{:});
D_NY(:,2) = data.Data_ED_7.HostFixedTimeLog.(field{:});
D_NY(:,3) = data.Data_ED_11.HostFixedTimeLog.(field{:});

out.ND_Y = ND_Y;
out.ND_NY = ND_NY;
out.D_Y = D_Y;
out.D_NY = D_NY;
end      

function out = calcSumGapAcceptance(data)
[max_size, ~] = max(cellfun('size', data, 1));
x = ceil((max(max_size))/50)*50;
out = zeros(x,3);
for j=1:size(data,2)
    for i=1:length(data)
        temp = data{i,j};
        out(1:length(temp),j) = out(1:length(temp),j) + temp;
    end
end
out = out*100/length(data);
end
function out = smoothData(data,factor) 
% factor = Number of data points for calculating the smoothed value|Default = 5
out = zeros(size(data));
for i = 1:size(data,2)
    out(:,i) = smooth(data(:,i),factor);
end
end

function out = calcMeanGroup(data)
% fill up the smaller arrays with the last known velocity, such that all
% arrrays have the same length.
[max_size, ~] = max(cellfun('size', data, 1));
x = ceil((max(max_size))/50)*50;
out = zeros(x,3);
for j=1:size(data,2)
    for i=1:length(data)
        temp = data{i,j};
        out(1:length(temp),j) = out(1:length(temp),j) + temp;
        for di = length(temp)+1:x
            out(di,j) = out(di,j) + temp(end);
        end
    end
end
out = out./length(data);
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