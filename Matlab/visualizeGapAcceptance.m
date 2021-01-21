%% Visualizee GapAcceptance
% This script vizualizes the gap acceptance data 
% Author: Johnson Mok
% Last Updated: 19-01-2021

function visualizeGapAcceptance(data)
[ND_Y, ND_NY, D_Y, D_NY] = getOrganizedDY(data);

% Summing gap acceptance values per variable combination
sum_ND_Y = calcSumGapAcceptance(ND_Y);
sum_ND_NY = calcSumGapAcceptance(ND_NY);
sum_D_Y = calcSumGapAcceptance(D_Y);
sum_D_NY = calcSumGapAcceptance(D_NY);

smooth_ND_Y = smoothData(sum_ND_Y,11);
smooth_ND_NY = smoothData(sum_ND_NY,11);
smooth_D_Y = smoothData(sum_D_Y,11);
smooth_D_NY = smoothData(sum_D_NY,11);

visSumGapAcceptance(smooth_ND_Y,smooth_ND_NY,smooth_D_Y,smooth_D_NY);



end

%% Helper functions
function [ND_Y, ND_NY, D_Y, D_NY] = getOrganizedDY(data)
% ND_Y: ED 0, 4, 8
ND_Y(:,1) = data.Data_ED_0.HostFixedTimeLog.gapAcceptance;
ND_Y(:,2) = data.Data_ED_4.HostFixedTimeLog.gapAcceptance;
ND_Y(:,3) = data.Data_ED_8.HostFixedTimeLog.gapAcceptance;

% ND_NY: ED 1, 5, 9
ND_NY(:,1) = data.Data_ED_1.HostFixedTimeLog.gapAcceptance;
ND_NY(:,2) = data.Data_ED_5.HostFixedTimeLog.gapAcceptance;
ND_NY(:,3) = data.Data_ED_9.HostFixedTimeLog.gapAcceptance;

% D_Y: ED 2, 6, 10
D_Y(:,1) = data.Data_ED_2.HostFixedTimeLog.gapAcceptance;
D_Y(:,2) = data.Data_ED_6.HostFixedTimeLog.gapAcceptance;
D_Y(:,3) = data.Data_ED_10.HostFixedTimeLog.gapAcceptance;

% D_NY: ED 1, 5, 9
D_NY(:,1) = data.Data_ED_3.HostFixedTimeLog.gapAcceptance;
D_NY(:,2) = data.Data_ED_7.HostFixedTimeLog.gapAcceptance;
D_NY(:,3) = data.Data_ED_11.HostFixedTimeLog.gapAcceptance;
end
function out = calcSumGapAcceptance(data)
[max_size, ~] = max(cellfun('size', data, 1));
x = ceil((max(max_size))/50)*50;
out = zeros(x,3);
for j=1:size(data,2)
    for i=1:length(data)
        temp = data{i,j};
        for k=1:length(temp)
            out(k,j) = out(k,j)+temp(k);
        end
    end
end
end
function out = smoothData(data,factor) 
% factor = Number of data points for calculating the smoothed value|Default = 5
out = zeros(size(data));
for i = 1:size(data,2)
    out(:,i) = smooth(data(:,i),factor);
end
end
function visSumGapAcceptance(sum_ND_Y,sum_ND_NY,sum_D_Y,sum_D_NY)
strMap = {'Baseline','Mapping 1','Mapping 2'};
dt = 0.0167;
data = {sum_ND_Y,sum_ND_NY,sum_D_Y,sum_D_NY}; 
titlestr = {'Gap Acceptance - No Distraction - Yielding';'Gap Acceptance - No Distraction - No yielding';...
    'Gap Acceptance - Distraction - Yielding'; 'Gap Acceptance - Distraction - No yielding'};
figure;
for i = 1:length(data)
    subplot(2,2,i)
    hold on;
    x = (1:length(data{i}))*dt;
    plot(x,data{i},'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('gap acceptance'); 
    legend(strMap); title(titlestr{i});
end
end
