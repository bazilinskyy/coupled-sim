%% Visualize Head Angle
% Author: Johnson Mok
% Last Updated: 24-02-2020

function visualizeHeadAngle(data, relyaw)
% histPosAngleAll(data.anglePerPhaseMat);
visAllAngle(data.all);

% histPosAngleAll_rel(relyaw.yawPerPhase);
visAllAnglePhase(relyaw.CountPerPhase);

%% Unused
% visAllAngle(data.mean);
% visAllAngle(data.index);
% visAllAngleV2(data.ind);
% barAngle(data.mean, 'ND_Y');
end

%% Helper function
function out = set90InMid(data1, data2, data3)  
maxX = max([max(data1), max(data2), max(data3)]);
minX = min([min(data1), min(data2), min(data3)]);
maxX = ceil(maxX/10)*10;
minX = floor(minX/10)*10;
mid = 90;
diffmax = abs(maxX-mid);
diffmin = abs(minX-mid);
maxdiff = max(diffmax, diffmin);
out = [mid-maxdiff mid+maxdiff];
end

function visAngle(dat, con)
fld_con = fieldnames(dat);
c = find(strcmp(fld_con,con));
data = dat.(fld_con{c});
titlestr = {'No Distraction - Yielding', 'No Distraction - No Yielding',...
    'Distraction - Yielding', 'Distraction - No Yielding'};
fld_map = fieldnames(data);
linesty = {'-','--','-.'};
% Largest value
maxvals = max([max(data.(fld_map{1}).freq); max(data.(fld_map{2}).freq); max(data.(fld_map{3}).freq)]);

% Plot
hold on
for m=1:length(fld_map)
    plot(data.(fld_map{m}).val, data.(fld_map{m}).freq,linesty{m},'LineWidth',2);
end
xline(90,'--');
xlabel('Degrees','FontSize',15,'FontWeight','bold');
ylabel({'Percentage of yaw';'occurence [%]'},'FontSize',15,'FontWeight','bold');
set(gca, 'XDir','reverse')
set(gca,'FontSize',15);
title(titlestr{c},'FontSize',15,'FontWeight','bold');
xticks([-180:45:180])
% xlim(set90InMid(data.(fld_map{1}).val,data.(fld_map{2}).val,data.(fld_map{3}).val));
% xlim([-10 190])
xlim([135 190])
yticks(0:1:ceil(maxvals));
ylim([0 ceil(maxvals)]);
grid on;
legend('baseline','GTY', 'LATY');
end

function visAllAngle(data)
fld_con = fieldnames(data);
figure;
for c=1:length(fld_con)
    subplot(2,2,c);
    visAngle(data, fld_con{c});
end
end

function visAngleV2(dat, con)
fld_con = fieldnames(dat);
c = find(strcmp(fld_con,con));
data = dat.(fld_con{c});
titlestr = {'No distraction - Yielding', 'No distraction - No yielding',...
    'Distraction - Yielding', 'Distraction - No yielding'};
fld_map = fieldnames(data);
hold on
for m=1:length(fld_map)
    plot(data.(fld_map{m}).val, data.(fld_map{m}).freq,'LineWidth',2);
end
xline(90,'--');
xlabel('Degrees','FontSize',15,'FontWeight','bold');
ylabel({'Percentage of time';'looking in [%]'},'FontSize',15,'FontWeight','bold');
set(gca, 'XDir','reverse')
set(gca,'FontSize',15);
title(titlestr{c},'FontSize',15,'FontWeight','bold');
xlim(set90InMid(data.(fld_map{1}).val,data.(fld_map{2}).val,data.(fld_map{3}).val));
ylim([0 7]);
grid on;
legend('baseline','Gaze to yield', 'Look away to yield');
end

function visAllAngleV2(data)
fld_con = fieldnames(data);
figure;
for c=1:length(fld_con)
    subplot(2,2,c);
    visAngleV2(data, fld_con{c});
end
end

function barAngle(dat, con)
fld_con = fieldnames(dat);
c = find(strcmp(fld_con,con));
data = dat.(fld_con{c});
titlestr = {'No Distraction - Yielding', 'No Distraction - No Yielding',...
    'Distraction - Yielding', 'Distraction - No Yielding'};
fld_map = fieldnames(data);
hold on
for m=1:length(fld_map)
    bar(data.(fld_map{m}).val, data.(fld_map{m}).freq);
end

% xline(90,'--');
xlabel('Degrees','FontSize',15,'FontWeight','bold');
ylabel({'Percentage of time';'looking in [%]'},'FontSize',15,'FontWeight','bold');
set(gca, 'XDir','reverse')
set(gca,'FontSize',15);
title(titlestr{c},'FontSize',15,'FontWeight','bold');
% xlim(set90InMid(data.(fld_map{1}).val,data.(fld_map{2}).val,data.(fld_map{3}).val));
% ylim([0 30]);
grid on;
legend('baseline','Gaze to yield', 'Look away to yield');
end

function histPosAngle(angle, con)
fld_con = fieldnames(angle);
c = find(strcmp(fld_con,con));
data = angle.(fld_con{c});
titlestr = {'ND - Y', 'ND - NY',...
    'D - Y', 'D - NY'};
mapstr = {'baseline','GTY','LATY'};
fld_map = fieldnames(data);
fld_phase = fieldnames(data.map0);

figure;
for m =1:length(fld_map)
    % Bin range
    binrng = -10:10:200;
    % Counts data
        counts1(:,m) = histc(data.(fld_map{m}).ph1, binrng);                                    
        counts2(:,m) = histc(data.(fld_map{m}).ph2, binrng); 
    if length(fld_phase)>2
        counts3(:,m) = histc(data.(fld_map{m}).ph3, binrng);
    end
end
counts1 = 100*counts1./sum(counts1);
counts2 = 100*counts2./sum(counts2);
if length(fld_phase)>2
    counts3 = 100*counts3./sum(counts3);
end
    
% maxcount = max([counts1+counts2],[],'all');
% if length(fld_phase)>2
%     maxcount = max([counts1+counts2+counts3],[],'all');
% end

for m =1:length(fld_map)
    % Plot
    subplot(1,3,m);
    if length(fld_phase)<3
        b = bar(binrng, [counts1(:,m)'; counts2(:,m)'], 'stacked');
        legend('25 m - 59.76 m', 'past zebra - 25 m');
    elseif length(fld_phase)>2
        b = bar(binrng, [counts1(:,m)'; counts2(:,m)'; counts3(:,m)'],'stacked');
        legend('25 m - 59.76 m', '6.23 m - 25 m', 'standstill 6.23 m');     
    end
    set(gca, 'XDir','reverse')
%     ylim([0 ceil(maxcount/500)*500])
    ylim([0 100])
    ylabel('Percentage of yaw occurence [%]','FontWeight','bold');
%     yticks([0:1000:ceil(maxcount/500)*500])
    yticks([0:10:100])
    xticks([-10:20:200])
    xlabel('yaw [deg]','FontWeight','bold');
    title(join([mapstr{m},'-',titlestr(c)]))
    set(gca, 'XTickLabelRotation', 45)
    set(gca,'FontSize',15);
    grid on;
end

end
function histPosAngleAll(angle)
fld_con = fieldnames(angle);
for c=1:length(fld_con)
    histPosAngle(angle, fld_con{c});
end
end

 function histPosAngle_rel(angle, con)
fld_con = fieldnames(angle);
c = find(strcmp(fld_con,con));
data = angle.(fld_con{c});
titlestr = {'ND - Y', 'ND - NY',...
    'D - Y', 'D - NY'};
mapstr = {'baseline','GTY','LATY'};
fld_map = fieldnames(data);
fld_phase = fieldnames(data.map0);

figure;
for m =1:length(fld_map)
    % Bin range
    binrng = -180:10:180;
    % Counts data
        counts1(:,m) = histc(data.(fld_map{m}).ph1, binrng);                                    
        counts2(:,m) = histc(data.(fld_map{m}).ph2, binrng); 
    if length(fld_phase)>2
        counts3(:,m) = histc(data.(fld_map{m}).ph3, binrng);
    end
end
counts1 = 100*counts1./sum(counts1);
counts2 = 100*counts2./sum(counts2);
if length(fld_phase)>2
    counts3 = 100*counts3./sum(counts3);
end
    
% maxcount = max([counts1+counts2],[],'all');
% if length(fld_phase)>2
%     maxcount = max([counts1+counts2+counts3],[],'all');
% end

for m =1:length(fld_map)
    % Plot
    subplot(1,3,m);
    if length(fld_phase)<3
        b = bar(binrng, [counts1(:,m)'; counts2(:,m)'], 'stacked');
        legend('25 m - 59.76 m', 'past zebra - 25 m');
    elseif length(fld_phase)>2
        b = bar(binrng, [counts1(:,m)'; counts2(:,m)'; counts3(:,m)'],'stacked');
        legend('25 m - 59.76 m', '6.23 m - 25 m', 'standstill 6.23 m');     
    end
    set(gca, 'XDir','reverse')
%     ylim([0 ceil(maxcount/500)*500])
%     ylim([0 100])
    ylabel('Percentage of yaw occurence [%]','FontWeight','bold');
%     yticks([0:1000:ceil(maxcount/500)*500])
%     yticks([0:10:100])
%     xticks([-10:20:200])
    xlabel('yaw [deg]','FontWeight','bold');
    title(join([mapstr{m},'-',titlestr(c)]))
    set(gca, 'XTickLabelRotation', 45)
    set(gca,'FontSize',15);
    grid on;
end

end
function histPosAngleAll_rel(angle)
fld_con = fieldnames(angle);
for c=1:length(fld_con)
    histPosAngle_rel(angle, fld_con{c});
end
end

function visAnglePhase(dat, con, phase)
fld_con = fieldnames(dat);
c = find(strcmp(fld_con,con));
data = dat.(fld_con{c});
fld_map = fieldnames(data);
fld_phase = fieldnames(data.(fld_map{1}));
p = find(strcmp(fld_phase,phase));

linesty = {'-','--','-.'};
titlestr = {'ND - Y', 'ND - NY',...
    'D - Y', 'D - NY'};
mapstr = {'baseline','GTY', 'LATY'};
if length(fld_phase) == 2
    phasestr = {'[25 m - 59.76 m]', '[past zebra - 25 m]'};
else
    phasestr = {'[25 m - 59.76 m]', '[6.23 m - 25 m]', '[standstill 6.23 m]'};
end
% Plot
    hold on
    % Largest value
    maxvals = max([max(data.(fld_map{1}).(fld_phase{p}).freq(1:end-1));...
        max(data.(fld_map{2}).(fld_phase{p}).freq(1:end-1)); max(data.(fld_map{3}).(fld_phase{p}).freq(1:end-1))]);
    for m=1:length(fld_map)
        plot(data.(fld_map{m}).(fld_phase{p}).val, data.(fld_map{m}).(fld_phase{p}).freq, linesty{m},'LineWidth',2);
        yl = yline(max(data.(fld_map{m}).(fld_phase{p}).freq(1:end-1)),'--',mapstr{m},'LabelHorizontalAlignment','left','LabelVerticalAlignment','middle');
        yl.Annotation.LegendInformation.IconDisplayStyle = 'off';
    end
    xlabel('Degrees','FontSize',15,'FontWeight','bold');
    ylabel({'Percentage of yaw','difference occurence [%]'},'FontSize',15,'FontWeight','bold');
    set(gca, 'XDir','reverse')
    set(gca,'FontSize',15);
    title(join([titlestr{c},' - ',phasestr{p}]),'FontSize',15,'FontWeight','bold');
%     xticks([-180:45:180])
    % xlim(set90InMid(data.(fld_map{1}).val,data.(fld_map{2}).val,data.(fld_map{3}).val));
    % xlim([-10 190])
%     xlim([135 190])
    yticks(0:2:ceil(maxvals));
    ylim([0 ceil(maxvals)]);
    grid on;
    legend('baseline','GTY', 'LATY');
end

function visAllAnglePhase(data)
fld_con = fieldnames(data);
for j= 1:2
    figure;
    i = 0;
    for c=[j, j+2] %length(fld_con) 
        fld_phase = fieldnames(data.(fld_con{c}).map0);
        for p =1:length(fld_phase)
            i = i+1;
            subplot(2,length(fld_phase),i);
            visAnglePhase(data, fld_con{c}, fld_phase{p});
        end
    end
end
end
