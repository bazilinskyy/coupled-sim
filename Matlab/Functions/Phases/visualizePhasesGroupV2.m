%% Visualizee Pos Group
% This script vizualizes the AV position separated by phases
% Author: Johnson Mok
% Last Updated: 10-02-2021

function visualizePhasesGroupV2(data)
%% data separate (Time not matched)
% visAllIndividualV2(data.borders, data.orgdata); % works

%% data grouped (Time matched)
% visAllIndividual(data.borders, data.grouped); 
visAllMean(data.borders, data.grouped); %--
% visAllMeanV2(data.borders, data.grouped);


end

%% Helper functions
function [labstr, drawPline, colour] = getLabel(con)
if(strcmp(con,'ND_Y'))
    labstr = 'No distraction - Yielding';
    drawPline = true;
    colour = [0, 0.4470, 0.7410];
elseif(strcmp(con,'D_Y'))
    labstr = 'Distraction - Yielding';
    drawPline = true;
    colour = [0.9290, 0.6940, 0.1250];
elseif(strcmp(con,'D_NY'))
    labstr = 'Distraction - No yielding';
    drawPline = false;
    colour = [0.4940, 0.1840, 0.5560];
elseif(strcmp(con,'ND_NY'))
    labstr = 'No distraction - No yielding';
    drawPline = false;
    colour = [0.8500, 0.3250, 0.0980];
end
end

% Separate
function labstr = visIndividualV2(border, data, con, mapping)
strMap = {'Baseline','Gaze to yield','Look away to yield'};
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}));
m = find(strcmp(fld_map,mapping));
fld_phase = fieldnames(data.(fld_con{c}).(fld_map{m}).pos);
[labstr, drawPline, colour] = getLabel(con);
% Draw
hold on
for i=1:length(fld_phase)
    plotdata = data.(fld_con{c}).(fld_map{m}).pos.(fld_phase{i});
    for j=1:length(plotdata)
        a = plot(plotdata{j}(:,1),plotdata{j}(:,2),'Color', colour,'LineWidth',1);
        % Label settings
        if(j>1 || i>1)
            set(get(get(a,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
        end
    end
    % Rectangle
    if(drawPline==true)
%         rectangle('Position',border.(fld_con{c}).(fld_map{m}).rect(i,:),...
%             'FaceColor',[0.9-i/10 0.9-i/10 0.9-i/10 0.4],'EdgeColor',[0 0 0]);
%         text(border.(fld_con{c}).(fld_map{m}).midx(i), 60,['(',num2str(i),')'],'HorizontalAlignment','center','VerticalAlignment', 'top');
    end

end
yl = yline(0,'--'); set(get(get(yl,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
grid on
title(join(['[',strMap(m),'] - ','Distance from pedestrian vs Time']),'FontSize',15,'FontWeight','bold');
xlabel('Time in [s]','FontSize',10,'FontWeight','bold');
ylabel({'Distance from'; 'pedestrian in [m]'},'FontSize',4,'FontWeight','bold');
ylim([border.(fld_con{1}).(fld_map{1}).rect(1,2), border.(fld_con{1}).(fld_map{1}).rect(1,2)+border.(fld_con{1}).(fld_map{1}).rect(1,4)]);
xlim([0, border.(fld_con{1}).(fld_map{1}).rect(end,1)+border.(fld_con{1}).(fld_map{1}).rect(end,3)]);
ax=gca; ax.FontSize = 15;
end
function visAllIndividualV2(borders, data)
fld_con = fieldnames(data);
for c=[1,3]
    fld_map = fieldnames(data.(fld_con{c}));
    figure;
    for m=1:length(fld_map)
        subplot(3,1,m)
        a = visIndividualV2(borders, data, (fld_con{c}), (fld_map{m}));
        b = visIndividualV2(borders, data, (fld_con{c+1}), (fld_map{m}));
        legend(a,b,'Location','southwest');
    end
end
end

% Grouped
function labstr = visIndividual(border, data, con, mapping)
strMap = {'Baseline','Gaze to yield','Look away to yield'};
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}).stdData);
m = find(strcmp(fld_map,mapping));
fld_phase = fieldnames(data.(fld_con{c}).stdData.(fld_map{m}));
[labstr, drawPline, colour] = getLabel(con);
temp = 0;
hold on;
% Plot 
for i = 1:length(fld_phase)
    % Set Data
    y = data.(fld_con{c}).grpos.(fld_map{m}).(fld_phase{i});
    x = (temp:temp+size(y,1)-1)*0.0167;
    % Set background colour
    if(drawPline==true)
        rectangle('Position',border.(fld_con{c}).(fld_map{m}).rect(i,:),...
            'FaceColor',[0.9-i/10 0.9-i/10 0.9-i/10 0.4],'EdgeColor',[0 0 0]);
        text(border.(fld_con{c}).(fld_map{m}).midx(i), 60,['(',num2str(i),')'],'HorizontalAlignment','center','VerticalAlignment', 'top');
    end
    % Plot Data
    a = plot(x, y, 'Color', colour);
    temp = temp+size(y,1);
    % Label settings
    if(i==1); start = 2;
    else; start = 1;
    end
    for j=start:size(y,2); set(get(get(a(j),'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
    end
end
yl = yline(0,'--'); set(get(get(yl,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
grid on;
title(join(['[',strMap(m),'] - ','Distance from pedestrian vs Time']),'FontSize',15,'FontWeight','bold');
xlabel('Time in [s]','FontSize',10,'FontWeight','bold');
ylabel({'Distance from'; 'pedestrian in [m]'},'FontSize',4,'FontWeight','bold');
ylim([border.(fld_con{1}).(fld_map{1}).rect(1,2), border.(fld_con{1}).(fld_map{1}).rect(1,2)+border.(fld_con{1}).(fld_map{1}).rect(1,4)]);
xlim([0, border.(fld_con{1}).(fld_map{1}).rect(end,1)+border.(fld_con{1}).(fld_map{1}).rect(end,3)]);
ax=gca; ax.FontSize = 15;
end
function visAllIndividual(borders, data)
fld_con = fieldnames(data);
for c=[1,3]
    fld_map = fieldnames(data.(fld_con{c}).grpos);
    figure;
    for m=1:length(fld_map)
        subplot(3,1,m)
        a = visIndividual(borders, data, (fld_con{c}), (fld_map{m}));
        b = visIndividual(borders, data, (fld_con{c+1}), (fld_map{m}));
        legend(a,b,'Location','southwest');
    end
end
end

function labstr = visMean(border, data, con, mapping)
strMap = {'Baseline','Gaze to yield','Look away to yield'};
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}).meanData);
m = find(strcmp(fld_map,mapping));
fld_phase = fieldnames(data.(fld_con{c}).meanData.(fld_map{m}));
[labstr, drawPline, colour] = getLabel(con);

hold on;
temp = 1;
for i = 1:length(fld_phase)
    if(drawPline==true)
        rectangle('Position',border.(fld_con{c}).(fld_map{m}).rect(i,:),...
            'FaceColor',[0.9-i/10 0.9-i/10 0.9-i/10 0.4],'EdgeColor',[0 0 0]);
        text(border.(fld_con{c}).(fld_map{m}).midx(i), 60,['(',num2str(i),')'],...
            'HorizontalAlignment','center','VerticalAlignment', 'top','FontSize',15,'FontWeight','bold');
    end
    err = data.(fld_con{c}).stdData.(fld_map{m}).(fld_phase{i});
    y = data.(fld_con{c}).meanData.(fld_map{m}).(fld_phase{i});
    yplus = y+err;
    ymin = y-err;
    x = (temp:temp+length(y)-1)*0.0167;
    a = plot(x, y, 'Color', colour,'LineWidth',2);
    hold on;
    b = plot(x, yplus, 'Color', colour,'LineWidth',2);
    d = plot(x, ymin, 'Color', colour,'LineWidth',2);
    temp = temp+length(y);
    % Label settings
        set(get(get(b,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
        set(get(get(d,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
    if(i==1); start = 2;
    else; start = 1;
    end
    for j=start:size(y,2); set(get(get(a(j),'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
    end
end
yl = yline(0,'--'); set(get(get(yl,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
grid on;
title(join(['[',strMap(m),'] - ','Distance from pedestrian vs Time']),'FontSize',15,'FontWeight','bold');
xlabel('Time in [s]','FontSize',10,'FontWeight','bold');
ylabel({'Distance from'; 'pedestrian in [m]'},'FontSize',4,'FontWeight','bold');
ylim([border.(fld_con{1}).(fld_map{1}).rect(1,2), border.(fld_con{1}).(fld_map{1}).rect(1,2)+border.(fld_con{1}).(fld_map{1}).rect(1,4)]);
xlim([0, border.(fld_con{1}).(fld_map{2}).rect(end,1)]);%+border.(fld_con{1}).(fld_map{1}).rect(end,3)]);
ax=gca; ax.FontSize = 15;
end
function visAllMean(borders, data)
fld_con = fieldnames(data);
for c=[1,3]
    fld_map = fieldnames(data.(fld_con{c}).meanData);
    figure;
    for m=1:length(fld_map)
        subplot(3,1,m)
        a = visMean(borders, data, (fld_con{c}), (fld_map{m}));
        b = visMean(borders, data, (fld_con{c+1}), (fld_map{m}));
        legend(a,b,'Location','southwest','FontSize',15,'FontWeight','bold');
    end
end
end
function visAllMeanV2(borders, data)
fld_con = fieldnames(data);
for c=1
    fld_map = fieldnames(data.(fld_con{c}).meanData);
    figure;
    for m=1:length(fld_map)
        subplot(3,1,m)
        a = visMean(borders, data, (fld_con{c}), (fld_map{m}));
        b = visMean(borders, data, (fld_con{c+1}), (fld_map{m}));
        d = visMean(borders, data, (fld_con{c+2}), (fld_map{m}));
        e = visMean(borders, data, (fld_con{c+3}), (fld_map{m}));
        legend(a,b,d,e,'Location','southwest','FontSize',15,'FontWeight','bold');
    end
end
end
