%% Visualizee GapAcceptance
% This script vizualizes the gap acceptance data 
% Author: Johnson Mok
% Last Updated: 04-02-2021

function visualizeGapAcceptanceV2(in, phase)
%% Phases
% visAllGapAcceptancePhases(in.phasesSum, phase.borders);
% visAllPerGapAcceptancePhases(in.phasesPer);
visNumberOfGapAcceptancePhasesAll(in.phasesSum, phase.borders);

visAllDecisionCertainty(in.DC)
%% OLD
% visSumGapAcceptance(in.smoothgap);
% 
% visGapAcceptVSother(in.sumGap, in.sumAVvel, 'AV velocity ','in [m/s]', [-0.5 35]);
% visGapAcceptVSother(in.sumGap, in.sumAVposz, 'AV z-position ','in [m]', [-50 80]);
%                
% visGapAcceptVSposped(in.sumGap, in.sumAVposz);

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
function labstr = visGapAcceptancePhases(data, border, con, mapping)
strMap = {'Baseline','Mapping 1','Mapping 2'};
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}));
m = find(strcmp(fld_map,mapping));
fld_phase = fieldnames(data.(fld_con{c}).(fld_map{m}));
[labstr, drawPline, colour] = getLabel(con);
% Phase sizes
t = zeros(length(fld_phase)+1,1);
for i=1:length(fld_phase)
    if i == 1
        t(i+1) = length(data.(fld_con{c}).(fld_map{m}).(fld_phase{i}));
    else
        t(i+1) = t(i) + length(data.(fld_con{c}).(fld_map{m}).(fld_phase{i}));
    end
end
% Draw
hold on
for i=1:length(fld_phase)
    plotdata = data.(fld_con{c}).(fld_map{m}).(fld_phase{i});
%     disp(size((t(i)+1:t(i+1))*0.0167)); disp(size(plotdata));
    a = plot((t(i)+1:t(i+1))*0.0167,plotdata,'Color', colour,'LineWidth',2);
        % Label settings
        if(i>1)
            set(get(get(a,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
        end
    % Rectangle
    if(drawPline==true)
        rectpos = [border.(fld_con{c}).(fld_map{m}).rect(i,1), -5, border.(fld_con{c}).(fld_map{m}).rect(i,3), 110];
        rectangle('Position',rectpos,'FaceColor',[0.9-i/10 0.9-i/10 0.9-i/10 0.4],'EdgeColor',[0 0 0]);
        text(border.(fld_con{c}).(fld_map{m}).midx(i), 105,['(',num2str(i),')'],'HorizontalAlignment','center','VerticalAlignment', 'top');
    end
grid on
ax=gca; ax.FontSize = 15;
title(join(['[',strMap(m),'] - ','Distance from pedestrian vs Time']),'FontSize',12,'FontWeight','bold');
xlabel('Time in [s]','FontSize',15,'FontWeight','bold');
ylabel({'Gap acceptance'; 'in [%]'},'FontSize',15,'FontWeight','bold');
ylim([-5 105]);
xlim([0, border.(fld_con{1}).(fld_map{1}).rect(end,1)+border.(fld_con{1}).(fld_map{1}).rect(end,3)]);
end
end
function visAllGapAcceptancePhases(data, border)
fld_con = fieldnames(data);
for c=[1,3]
    fld_map = fieldnames(data.(fld_con{c}));
    figure;
    for m=1:length(fld_map)
        subplot(3,1,m)
        a = visGapAcceptancePhases(data, border, fld_con{c}, fld_map{m}); 
        b = visGapAcceptancePhases(data, border, fld_con{c+1}, fld_map{m}); 
        legend(a,b,'Location','northeast');
    end
end
end

function visPerGapAcceptancePhases(dataY, conY, dataNY, conNY)
[labstrY, ~, ~] = getLabel(conY);
[labstrNY, ~, ~] = getLabel(conNY);
strmap = {'[Baseline]', '[Mapping 1]', '[Mapping 2]'};
colours = [0, 0.4470, 0.7410; 0.8500, 0.3250, 0.0980; 0.9290, 0.6940, 0.1250];
fld_map = fieldnames(dataY);
hold on;
for m=1:length(fld_map) 
    str1 = join([strmap(m), labstrY]);
    str2 = join([strmap(m), labstrNY]);
    plot(dataY.(fld_map{m}),'Color',colours(m,:),'DisplayName',str1{:},'LineWidth',2);
    plot(dataNY.(fld_map{m}),'--','Color',colours(m,:),'DisplayName',str2{:},'LineWidth',2);
    ax=gca; ax.FontSize = 15;
    xticks(1:5)
    xticklabels({'phase 1','phase 2','phase 3','phase 4','phase 5'})
    xlabel('Phase','FontSize',15,'FontWeight','bold');
    ylabel('Gap Acceptance Phase in [%]','FontSize',15,'FontWeight','bold');
    title('Gap acceptance percentage per phase','FontSize',15,'FontWeight','bold');
    ylim([0 100]);
    grid on;
    legend('Location','southwest');
end
end
function visAllPerGapAcceptancePhases(data)
fld_con = fieldnames(data);
for c=[1,3]%length(fld_con)
    figure;
    visPerGapAcceptancePhases(data.(fld_con{c}), fld_con{c}, data.(fld_con{c+1}), fld_con{c+1});
end
end

function [line,colour,title,label] = getLineprop(con, map)
% Condition
if(strcmp(con,'ND_Y'))
    lineprop = '-';
    title = '[No distraction] - ';
    label = 'Yielding';
elseif(strcmp(con,'D_Y'))
    lineprop = '-';
    title = '[Distraction] - ';
    label = 'Yielding';
elseif(strcmp(con,'D_NY'))
    lineprop = '--';
    title = '[Distraction] - ';
    label = 'No yielding';
elseif(strcmp(con,'ND_NY'))
    lineprop = '--';
    title = '[No distraction] - ';
    label = 'No yielding';
end
% Mapping
if(strcmp(map,'map0'))
    marker = '';
    colour = [0, 0.4470, 0.7410];
elseif(strcmp(map,'map1'))
    marker = ''; %'o';
    colour = [0.8500, 0.3250, 0.0980];
elseif(strcmp(map,'map2'))
    marker = ''; %'x';
    colour = [0.9290, 0.6940, 0.1250];
end
line = join([lineprop,marker]);
end
function lab = visNumberOfGapAcceptancePhases(data, border, con, mapping)
strMap = {'Baseline','Gaze to yield','Look away to yield'};
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}));
m = find(strcmp(fld_map,mapping));
fld_phase = fieldnames(data.(fld_con{c}).(fld_map{m}));
nrPhases = length(fld_phase);
% Get lineproperties
[line,colour,titlestring,labstr] = getLineprop(fld_con{c}, fld_map{m});
lab = join(['[',strMap{m},'] - ',labstr]);
% Phase sizes
border_lim = nrPhases;
t = zeros(nrPhases+1,1);
for i=1:nrPhases %length(fld_phase)
    if i == 1
        t(i+1) = length(data.(fld_con{c}).(fld_map{m}).(fld_phase{i}));
    else
        t(i+1) = t(i) + length(data.(fld_con{c}).(fld_map{m}).(fld_phase{i}));
    end
end
% Draw
hold on
for i=1:nrPhases %length(fld_phase)
    plotdata = data.(fld_con{c}).(fld_map{m}).(fld_phase{i});
    a = plot((t(i)+1:t(i+1))*0.0167,plotdata,line,'Color', colour,'LineWidth',2);
        % Label settings
        if(i>1)
            set(get(get(a,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
        end
    % Rectangle
    if((strcmp(con,'ND_Y')||strcmp(con,'D_Y')) && strcmp(mapping,'map1'))
        rectpos = [border.(fld_con{c}).(fld_map{m}).rect(i,1), -5, border.(fld_con{c}).(fld_map{m}).rect(i,3), 110];
        rectangle('Position',rectpos,'FaceColor',[0.9-i/10 0.9-i/10 0.9-i/10 0.4],'EdgeColor',[0 0 0]);
        text(border.(fld_con{c}).(fld_map{m}).midx(i), 105,['(',num2str(i),')'],'HorizontalAlignment','center','VerticalAlignment', 'top');
    end
grid on
ax=gca; ax.FontSize = 15;
title(join([titlestring,'Distance from pedestrian vs Time']),'FontSize',18,'FontWeight','bold');
xlabel('Time in [s]','FontSize',15,'FontWeight','bold');
ylabel({'Crossing button press'; 'in [%]'},'FontSize',15,'FontWeight','bold');
ylim([-5 105]);
xlim([0, border.(fld_con{1}).(fld_map{2}).rect(border_lim,1)+border.(fld_con{1}).(fld_map{2}).rect(border_lim,3)]);
end
end
function visNumberOfGapAcceptancePhasesAll(data, border)
fld_con = fieldnames(data);
a = cell(3,1);
b = cell(3,1);
for c=[1,3]
    fld_map = fieldnames(data.(fld_con{c}));
    figure;
    for m=1:length(fld_map)
        a{m,:} = visNumberOfGapAcceptancePhases(data, border, fld_con{c}, fld_map{m}); 
    end
    for m=1:length(fld_map)
        b{m,:} = visNumberOfGapAcceptancePhases(data, border, fld_con{c+1}, fld_map{m}); 
    end
    legend([a;b],'Location','southwest');
end
end

function visDecisionCertainty(data, con)
strmap = {'Baseline','Mapping 1','Mapping 2'};
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}));

meandata = zeros(length(fld_map),1);
err = zeros(length(fld_map),1);
for m=1:length(fld_map)
    meandata(m) = data.(fld_con{c}).(fld_map{m}).mean;
    err(m) = data.(fld_con{c}).(fld_map{m}).std;
end

[labstr, ~, ~] = getLabel(con);
x = categorical(strmap);
x = reordercats(x,strmap);
titstr = join(['[',labstr,'] - Crossing decision reversals']);
colourcodes = [0, 0.4470, 0.7410; 0.8500, 0.3250, 0.0980; 0.9290, 0.6940, 0.1250];
% Bar graph
for i = 1:length(x)
h = bar(x(i),meandata(i));
set(h, 'FaceColor', colourcodes(i,:));
a = get(gca,'XTickLabel');
set(gca,'XTickLabel',a,'FontSize',18,'FontWeight','bold');
ylabel('Crossing decision reversals','FontSize',18,'FontWeight','bold');
title(titstr,'FontSize',18,'FontWeight','bold');
hold on;
grid on;
end
% Mean line
plot(x,meandata,'LineWidth',2);
% Errorbar
er = errorbar(x,meandata,err,'CapSize',20);
er.Color = [0 0 0];
er.LineStyle = 'none';
hold off;
ylim([0 5]);
end
function visAllDecisionCertainty(data)
fld_con = fieldnames(data);
for c=[1,3]%length(fld_con)
    figure;
    subplot(1,2,1);
    visDecisionCertainty(data,fld_con{c});
    subplot(1,2,2);
    visDecisionCertainty(data,fld_con{c+1});
end
end
%% Helper functions - OLD
function visSumGapAcceptance(data)
strMap = {'Baseline','Mapping 1','Mapping 2'};
dt = 0.0167;
fld_con = fieldnames(data);
titlestr = {'Gap Acceptance - No Distraction - Yielding';'Gap Acceptance - No Distraction - No yielding';...
    'Gap Acceptance - Distraction - Yielding'; 'Gap Acceptance - Distraction - No yielding'};
figure;
for i = 1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{i}));
    subplot(2,2,i)
    hold on;
    x = (1:length(data.(fld_con{i}).(fld_map{1}).gapAcceptance))*dt;
    plot(x,[data.(fld_con{i}).(fld_map{1}).gapAcceptance, data.(fld_con{i}).(fld_map{2}).gapAcceptance, data.(fld_con{i}).(fld_map{3}).gapAcceptance],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('gap acceptance in [%]'); 
    ylim([-0.5 105]);
    legend(strMap); title(titlestr{i});
end
end
function visGapAcceptVSother(data, v, ystr, ystrUnit, ylimit)
strMap = {'Baseline','Mapping 1','Mapping 2'};
dt = 0.0167;
gapstr = 'Gap Acceptance ';
ylab = join([ystr,ystrUnit]);
titlestr = {'- No Distraction - Yielding';'- No Distraction - No yielding';...
    '- Distraction - Yielding'; '- Distraction - No yielding'};

fld_con = fieldnames(data);
fld_map = fieldnames(data.(fld_con{1}));
fld_val = fieldnames(data.(fld_con{1}).(fld_map{1}));
fld_conv = fieldnames(v);
fld_mapv = fieldnames(v.(fld_conv{1}));
fld_valv = fieldnames(v.(fld_conv{1}).(fld_mapv{1}));
for c = 1:2
    figure
    subplot(2,2,1)
    x = (1:length(data.(fld_con{c}).(fld_map{1}).(fld_val{1})))*dt;
    plot(x,[data.(fld_con{c}).(fld_map{1}).(fld_val{1}), data.(fld_con{c}).(fld_map{2}).(fld_val{1}), data.(fld_con{c}).(fld_map{3}).(fld_val{1})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('gap acceptance in [%]'); 
    ylim([-0.5 105]);
    legend(strMap); title(join([gapstr,titlestr{c}]));
    
    subplot(2,2,2)
    x = (1:length(data.(fld_con{c+2}).(fld_map{1}).(fld_val{1})))*dt;
    plot(x,[data.(fld_con{c+2}).(fld_map{1}).(fld_val{1}), data.(fld_con{c+2}).(fld_map{2}).(fld_val{1}), data.(fld_con{c+2}).(fld_map{3}).(fld_val{1})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('gap acceptance in [%]'); 
    ylim([-0.5 105]);
    legend(strMap); title(join([gapstr,titlestr{c+2}]));
    
    subplot(2,2,3)
    xv = (1:length(v.(fld_conv{c}).(fld_mapv{1}).(fld_valv{1})))*dt;
    plot(xv,[v.(fld_conv{c}).(fld_mapv{1}).(fld_valv{1}), v.(fld_conv{c}).(fld_mapv{2}).(fld_valv{1}), v.(fld_conv{c}).(fld_mapv{3}).(fld_valv{1})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel(ylab);
    ylim(ylimit);
    legend(strMap); title(join([ystr,titlestr{c}]));
    
    subplot(2,2,4)
    xv = (1:length(v.(fld_conv{c+2}).(fld_mapv{1}).(fld_valv{1})))*dt;
    plot(xv,[v.(fld_conv{c+2}).(fld_mapv{1}).(fld_valv{1}), v.(fld_conv{c+2}).(fld_mapv{2}).(fld_valv{1}), v.(fld_conv{c+2}).(fld_mapv{3}).(fld_valv{1})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel(ylab);
    ylim(ylimit);
    legend(strMap); title(join([ystr,titlestr{c+2}]));
end
end
function visGapAcceptVSposped(data,v)
strMap = {'Baseline','Mapping 1','Mapping 2'};
gapstr = 'Gap Acceptance ';
titlestr = {'- No Distraction - Yielding';'- No Distraction - No yielding';...
    '- Distraction - Yielding'; '- Distraction - No yielding'};
fld_con = fieldnames(data);
fld_map = fieldnames(data.(fld_con{1}));
fld_val = fieldnames(data.(fld_con{1}).(fld_map{1}));
fld_conv = fieldnames(v);
fld_mapv = fieldnames(v.(fld_conv{1}));
fld_valv = fieldnames(v.(fld_conv{1}).(fld_mapv{1}));

posped = 17.19;
figure;
for c = 1:length(fld_con)
    subplot(2,2,c);
    hold on;
    for m=1:length(fld_map)
        x = v.(fld_conv{c}).(fld_mapv{m}).(fld_valv{1})-posped;
        plot(x,data.(fld_con{c}).(fld_map{m}).(fld_val{1}),'LineWidth',2);
    end
    set(gca, 'XDir','reverse'); ylim([0 100]);
    xlabel('Distance till pedestrian in [m]'); ylabel('gap acceptance in [%]');
    title(join([gapstr,titlestr{c}]));
    legend(strMap); grid on; 
end
end
