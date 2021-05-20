%% Visualize crossing performance
% Crossing performance as a function of acceptance rating

% Author: Johnson Mok
% Last Updated: 13-03-2021
function visualizeCrossingPerformance(data)
plotAll(data.score2, data.acpt, data.Mean_score2);

%%
% % ND_Y
% figure 
% subplot(1,4,1)
% plotPositive(data);
% 
% % ND_NY
% subplot(1,4,2)
% plotNegative(data,'ND_NY');
% 
% % D_Y
% subplot(1,4,3)
% plotNegative(data,'D_Y');
% 
% % D_NY
% subplot(1,4,4)
% plotNegative(data,'D_NY');
end

%% Helper functions
function plotPositive(data)
fld_map = fieldnames(data.score2.ND_Y);
colour = [0, 0.4470, 0.7410;...
0.8500, 0.3250, 0.0980;...
0.9290, 0.6940, 0.1250];
% find xlim
xdata = zeros(size(fld_map));
for m=1:length(fld_map)
    xdata(m) = data.acpt.(fld_map{m})(1);
end

hold on
for m=1:length(fld_map)
    X = data.acpt.(fld_map{m})(1);
    Y = data.score2.ND_Y.(fld_map{m}).mean;
    ystd = data.score2.ND_Y.(fld_map{m}).std;
    ystdmin = Y-data.score2.ND_Y.(fld_map{m}).p25;
    ystdplus = data.score2.ND_Y.(fld_map{m}).p75-Y;
    xstd = 0; %data.acpt.(fld_map{m})(2);
    
%     p = errorbar(X,Y,ystd,ystd,xstd,xstd,'o','MarkerSize',10,'CapSize',28);
    p = errorbar(X,Y,ystdmin,ystdplus,xstd,xstd,'o','MarkerSize',10,'CapSize',28);
    c = colour(m,:); %get(p,'Color');
    p.Color = colour(m,:);
    p.MarkerFaceColor = c;
    p2 = plot([X-xstd, X+xstd], [Y, Y],'LineWidth',2);
    p2.Color = c;
    set(get(get(p2,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
    p3 = plot([X, X], [Y-ystdmin, Y+ystdplus],'LineWidth',2);
    p3.Color = c;
    set(get(get(p3,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
end
legend('Baseline', 'Gaze to Yield', 'Look Away to Yield','Location','southwest');
grid on;
xlabel('Mean acceptance score','FontSize',15,'FontWeight','bold'); xlim([-2 2]); 
ylabel('Mean performance score','FontSize',15,'FontWeight','bold'); ylim([-2 2]); 
title('No Distraction - Yielding','FontSize',15,'FontWeight','bold');
ax=gca; ax.FontSize = 15;
xticks([0:0.5:1.5]);   % xticks([-0.5:0.5:2])
xlim([0 1.5]);         % xlim([-0.5 2])
ylim([20 100])
end

function plotNegative(data, condition)
fld_con = fieldnames(data.score2);
con = find(strcmp(fld_con,condition));
fld_map = fieldnames(data.score2.(fld_con{con}));
titlestr = {'No Distraction - Yielding','No Distraction - No Yielding', 'Distraction - Yielding','Distraction - No Yielding'};
colour = [0, 0.4470, 0.7410;...
0.8500, 0.3250, 0.0980;...
0.9290, 0.6940, 0.1250];

% find xlim
xdata = zeros(size(fld_map));
for m=1:length(fld_map)
    xdata(m) = data.acpt.(fld_map{m})(1);
end

hold on
for m=1:length(fld_map)
    X = data.acpt.(fld_map{m})(1);
    Y = data.score2.(fld_con{con}).(fld_map{m}).mean;
    ystd = data.score2.(fld_con{con}).(fld_map{m}).std;
    ystdmin = Y-data.score2.(fld_con{con}).(fld_map{m}).p25;
    ystdplus = data.score2.(fld_con{con}).(fld_map{m}).p75-Y;
    xstd = 0; %data.acpt.(fld_map{m})(2);
    
    p = errorbar(X,Y,ystdmin,ystdplus,xstd,xstd,'o','MarkerSize',10,'CapSize',28);
    c = colour(m,:); %get(p,'Color');
    p.Color = colour(m,:);
    p.MarkerFaceColor = c;
    p2 = plot([X-xstd, X+xstd], [Y, Y],'LineWidth',2);
    p2.Color = c;
    set(get(get(p2,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
    p3 = plot([X, X], [Y-ystdmin, Y+ystdplus],'LineWidth',2);
    p3.Color = c;
    set(get(get(p3,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
end
legend('Baseline', 'Gaze to Yield', 'Look Away to Yield','Location','southwest');
grid on;
xlabel('Mean acceptance score','FontSize',15,'FontWeight','bold'); xlim([-2 2]); 
ylabel('Mean performance score','FontSize',15,'FontWeight','bold'); ylim([-2 2]); 
title(titlestr{con},'FontSize',15,'FontWeight','bold');
ax=gca; ax.FontSize = 15;
xticks([0:0.5:1.5]);   % xticks([-0.5:0.5:2])
xlim([0 1.5]);         % xlim([-0.5 2])
ylim([20 100])
end

function [colour, marker, name, hide_legend] = markerPropSelector(con, map)
% col = {'red','green','cyan','blue'};
col = [0, 0.4470, 0.7410; 0.8500, 0.3250, 0.0980; 0.9290, 0.6940, 0.1250; 0.4940, 0.1840, 0.5560];
mark = {'o','>','s','d'};
mapstr = {'baseline - ', 'GTY - ', 'LATY - '};
constr = {'ND-Y','ND-NY','D-Y','D-NY'};

name = [constr{con}];
colour = col(con,:);
marker = mark{con};

hide_legend = false;
if map ~= 1
    hide_legend = true;
end
end

function plotAll(data,acpt,mean)
figure;
hold on;
grid on;
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        % Plot per condition per mapping
        [col, mark,name, hide_legend] = markerPropSelector(c, m);
            h = plot(acpt.(fld_map{m})(1), data.(fld_con{c}).(fld_map{m}).mean,...
                mark,'MarkerFaceColor',col,'MarkerEdgeColor','w','MarkerSize',15,'DisplayName',name);
            if hide_legend == true
                set(get(get(h,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
            end
    end
end
% Mapping line
x1 = xline(acpt.(fld_map{1})(1),'-.','baseline','LineWidth',2,'FontSize',15,'LabelHorizontalAlignment','left','FontWeight','bold'); 
x2 = xline(acpt.(fld_map{2})(1),'-.','GTY','LineWidth',2,'FontSize',15,'LabelHorizontalAlignment','left','FontWeight','bold'); 
x3 = xline(acpt.(fld_map{3})(1),'-.','LATY','LineWidth',2,'FontSize',15,'LabelHorizontalAlignment','left','FontWeight','bold'); 
set(get(get(x1,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
set(get(get(x2,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
set(get(get(x3,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');

% Plot mean per mapping
ac = [acpt.map0(1);acpt.map2(1);acpt.map1(1)];
me = [mean(1); mean(3); mean(2)];
plot(ac,me,'*-','DisplayName','Mean of all conditions','LineWidth',2);

xlabel('Mean acceptance score','FontSize',15,'FontWeight','bold'); 
ylabel('Mean performance score','FontSize',15,'FontWeight','bold'); 
legend('location','northwest');
ax=gca; ax.FontSize = 15;
end
