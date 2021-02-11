%% Visualizee Pos Group
% This script vizualizes the AV position separated by phases
% Author: Johnson Mok
% Last Updated: 10-02-2021

function visulizePhasesGroup(data)
%% data separate
% visAllIndividualV2(data.borders, data.orgdata);

%% data grouped
visAllIndividual(data.borders, data.grouped)

visMean(data.grouped)
end

%% Helper functions
% Separate
function labstr = visIndividualV2(border, data, con, mapping)
strMap = {'Baseline','Mapping 1','Mapping 2'};
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}));
m = find(strcmp(fld_map,mapping));
fld_phase = fieldnames(data.(fld_con{c}).(fld_map{m}).pos);
% label
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
        rectangle('Position',border.(fld_con{c}).(fld_map{m}).rect(i,:),...
            'FaceColor',[0.9-i/10 0.9-i/10 0.9-i/10 0.4],'EdgeColor',[0 0 0]);
        text(border.(fld_con{c}).(fld_map{m}).midx(i), 60,['(',num2str(i),')'],'HorizontalAlignment','center','VerticalAlignment', 'top');
    end

end
yl = yline(0,'--'); set(get(get(yl,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
grid on
title(join(['[',strMap(m),'] - ','Distance from pedestrian vs Time']));
xlabel('Time in [s]');
ylabel('Distance from pedestrian in [m]');
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
        legend(a,b);
    end
end
end

% Grouped
function labstr = visIndividual(border, data, con, mapping)
strMap = {'Baseline','Mapping 1','Mapping 2'};
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}).stdData);
m = find(strcmp(fld_map,mapping));
fld_phase = fieldnames(data.(fld_con{c}).stdData.(fld_map{m}));
% label
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
title(join(['[',strMap(m),'] - ','Distance from pedestrian vs Time']));
xlabel('Time in [s]');
ylabel('Distance from pedestrian in [m]');
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
        legend(a,b);
    end
end
end
function visMean(data)
fld_phase = fieldnames(data.ND_Y.stdData.map0);
temp = 1;
figure;
hold on;
for i = 1:length(fld_phase)
    err = data.ND_Y.stdData.map0.(fld_phase{i});
    y = data.ND_Y.meanData.map0.(fld_phase{i});
    x = (temp:temp+length(y)-1)*0.0167;
    plot(x, y);
    errorbar(x, y, err)
    temp = temp+length(y);
end
end

