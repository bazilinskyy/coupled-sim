%% visualizeGazeTime
% This script vizualizes the gaze data from the pedestrian and the
% passenger
% Author: Johnson Mok
% Last Updated: 18-02-2021

function visualizeGazeTime(data)
% visGroupCounts(data.groupcounts, 'ND_Y', 'map1', 'pa')
visAllGroupCounts(data.groupcounts)
end

%% Helper functions
function [titlestr,ylab,labelstr,colour,yielding] = getTitleString(con, mapping, person)
if(strcmp(con,'ND_Y'))
    constr = 'No distraction - Yielding';
    colour = [0, 0.4470, 0.7410];
    yielding = true;
elseif(strcmp(con,'D_Y'))
    constr = 'Distraction - Yielding';
    colour = [0.9290, 0.6940, 0.1250];
    yielding = true;
elseif(strcmp(con,'D_NY'))
    constr = 'Distraction - No yielding';
    colour = [0.4940, 0.1840, 0.5560];
    yielding = false;
elseif(strcmp(con,'ND_NY'))
    constr = 'No distraction - No yielding';
    colour = [0.8500, 0.3250, 0.0980];
    yielding = false;
end

if(strcmp(mapping,'map0'))
    mapstr = '[Baseline] ';
elseif(strcmp(mapping,'map1'))
    mapstr = '[Mapping 1] ';
elseif(strcmp(mapping,'map2'))
    mapstr = '[Mapping 2] ';
end

if(strcmp(person,'pa'))
    perstr = 'passenger gazing pedestrian';
    ylab = {'Percentage of'; 'passengers gazing'};% {'Percentage of the passengers'; 'gazing at the pedestrian'};
elseif(strcmp(person,'pe'))
    perstr = 'pedestrian gazing passenger';
    ylab = {'Percentage of'; 'pedestrians gazing'}; %{'Percentage of the pedestrians'; 'gazing at the AV'};
end
% out = join([mapstr,perstr,constr]);
titlestr = {join([mapstr,perstr])};
labelstr = constr;
end

function lab = visGroupCounts(data, con, mapping, person)
fld_con     = fieldnames(data);
c           = find(strcmp(fld_con,con));
fld_map     = fieldnames(data.(fld_con{c}));
m           = find(strcmp(fld_map,mapping));
fld_person  = fieldnames(data.(fld_con{c}).(fld_map{m}));
per         = find(strcmp(fld_person,person));
fld_phase   = fieldnames(data.(fld_con{c}).(fld_map{m}).(fld_person{per}));
% Titlestring
[titlestr,ylabelstr,lab,colour,yielding] = getTitleString(con, mapping, person);
% Rect data
    rectdata = zeros(length(fld_phase)-1, 4);
    midx = zeros(length(fld_phase)-1,1);
    for p=1:length(fld_phase)-1
        xdata = data.(fld_con{c}).(fld_map{m}).(fld_person{per}).(fld_phase{p}).val;
        if(length(xdata)>2)
            rectdata(p,:) = [xdata(3), 0, xdata(end)-xdata(3), 100];
            midx(p) = xdata(3)+(xdata(end)-xdata(3))/2;
        end
    end
    rectdata(1,3) = 40;
% Phase setting
if (length(fld_phase) == 6)
    minus = 2;
else 
    minus = 1;
end
% Plot
hold on;
for p=1:length(fld_phase)-minus
    xdata = data.(fld_con{c}).(fld_map{m}).(fld_person{per}).(fld_phase{p}).val;
    ydata = data.(fld_con{c}).(fld_map{m}).(fld_person{per}).(fld_phase{p}).freq;
    if(length(xdata)>2)
        a = plot(xdata(3:end),ydata(3:end),'LineWidth',2,'Color',colour); % start from 3 to avoid '-1/-8' and '0'
    end
    rectangle('Position',rectdata(p,:),'FaceColor',[0.9-p/10 0.9-p/10 0.9-p/10 0.4],'EdgeColor',[0 0 0]);
    text(midx(p), 100,['(',num2str(p),')'],'HorizontalAlignment','center','VerticalAlignment', 'top');
    if(p>1)
    	set(get(get(a,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
    end
end
set(gca, 'XDir','reverse'); grid on;
if (yielding == true)
    xlim([rectdata(4,1) rectdata(1,1)+rectdata(1,3)]);
else
    xlim([0 rectdata(1,1)+rectdata(1,3)]);
end
ax=gca; ax.FontSize = 15;
xlabel('AV distance from pedestrian in [m]','FontSize',15,'FontWeight','bold'); 
ylabel(ylabelstr,'FontSize',15,'FontWeight','bold'); ylim([0 100]);
title(titlestr,'FontSize',15,'FontWeight','bold');
end

function visAllGroupCounts(data)
fld_person = fieldnames(data.D_NY.map0);
fld_con = fieldnames(data);
% Yielding cases
idx(1,:) = [find(strcmp(fld_con,'ND_Y')), find(strcmp(fld_con,'D_Y'))];
idx(2,:) = [find(strcmp(fld_con,'ND_NY')), find(strcmp(fld_con,'D_NY'))];
% Plot
for per=1:length(fld_person)
    for c=1:length(idx)
        fld_map = fieldnames(data.(fld_con{c}));
        figure;
        for m=1:length(fld_map)
            subplot(3,1,m);
            hold on;
            a = visGroupCounts(data, fld_con{idx(c,1)}, fld_map{m}, fld_person{per});
            b = visGroupCounts(data, fld_con{idx(c,2)}, fld_map{m}, fld_person{per});
            legend(a,b,'Location','northwest');
        end
    end
end
end