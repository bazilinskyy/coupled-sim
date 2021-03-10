%% Visualize Head Angle
% Author: Johnson Mok
% Last Updated: 24-02-2020

function visualizeHeadAngle(data)
visAllAngle(data.mean);
visAllAngleV2(data.ind);
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
ylim([0 30]);
grid on;
legend('baseline','Gaze to yield', 'Look away to yield');
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