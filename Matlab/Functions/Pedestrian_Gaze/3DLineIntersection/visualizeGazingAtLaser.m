%% visualizeGazingAtLaser
% Author: Johnson Mok
function visualizeGazingAtLaser(data_dis, data_ind, PeGazeAtAV)
visAllDistance(data_dis, PeGazeAtAV);
% visAllInd(data_ind);

end

%% Helper function
function visDistance(dat, con)
fld_con = fieldnames(dat);
c = find(strcmp(fld_con,con));
data = dat.(fld_con{c});
titlestr = {'No Distraction - Yielding', 'No Distraction - No Yielding',...
    'Distraction - Yielding', 'Distraction - No Yielding'};
fld_map = fieldnames(data);
hold on
for m=1:length(fld_map)
    plot(data.(fld_map{m}).val, data.(fld_map{m}).freq,'LineWidth',2);
end
xlabel('AV-pedestrian distance in [m]','FontSize',15,'FontWeight','bold');
ylabel({'Percentage of pedstrians';'looking at laser [%]'},'FontSize',15,'FontWeight','bold');
set(gca, 'XDir','reverse')
set(gca,'FontSize',15);
title(titlestr{c},'FontSize',15,'FontWeight','bold');
xlim([0 60]);
% ylim([0 30]);
grid on;
legend('baseline','Gaze to yield', 'Look away to yield','location','northwest');
end

function visDistanceAV(dat, con)
fld_con = fieldnames(dat);
c = find(strcmp(fld_con,con));
data = dat.(fld_con{c});
titlestr = {'No Distraction - Yielding', 'No Distraction - No Yielding',...
    'Distraction - Yielding', 'Distraction - No Yielding'};
fld_map = fieldnames(data);
hold on
for m=1:length(fld_map)
    plot(data.(fld_map{m}).val, data.(fld_map{m}).freq,'LineWidth',2);
end
xlabel('AV-pedestrian distance in [m]','FontSize',15,'FontWeight','bold');
ylabel({'Percentage of pedstrians';'looking at AV [%]'},'FontSize',15,'FontWeight','bold');
set(gca, 'XDir','reverse')
set(gca,'FontSize',15);
title(titlestr{c},'FontSize',15,'FontWeight','bold');
xlim([0 60]);
% ylim([0 30]);
grid on;
legend('baseline','Gaze to yield', 'Look away to yield','location','northwest');
end

function visAllDistance(data, PeGazeAtAV)
fld_con = fieldnames(data);
figure;
for c=1:length(fld_con)
    subplot(2,4,c);
    visDistance(data, fld_con{c});
    subplot(2,4,c+4);
    visDistanceAV(PeGazeAtAV, fld_con{c});
end
end

function visInd(dat, con)
fld_con = fieldnames(dat);
c = find(strcmp(fld_con,con));
data = dat.(fld_con{c});
titlestr = {'No Distraction - Yielding', 'No Distraction - No Yielding',...
    'Distraction - Yielding', 'Distraction - No Yielding'};
fld_map = fieldnames(data);
hold on
for m=1:length(fld_map)
    plot(data.(fld_map{m}).val, data.(fld_map{m}).freq,'LineWidth',2);
end
xlabel('Index','FontSize',15,'FontWeight','bold');
ylabel({'Percentage of pedstrians';'looking at laser[%]'},'FontSize',15,'FontWeight','bold');
set(gca,'FontSize',15);
title(titlestr{c},'FontSize',15,'FontWeight','bold');
% xlim(set90InMid(data.(fld_map{1}).val,data.(fld_map{2}).val,data.(fld_map{3}).val));
% ylim([0 30]);
grid on;
legend('baseline','Gaze to yield', 'Look away to yield');
end

function visAllInd(data)
fld_con = fieldnames(data);
figure;
for c=1:length(fld_con)
    subplot(2,2,c);
    visInd(data, fld_con{c});
end
end