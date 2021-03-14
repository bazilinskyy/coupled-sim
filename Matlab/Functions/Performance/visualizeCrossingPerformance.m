%% Visualize crossing performance
% Crossing performance as a function of acceptance rating

% Author: Johnson Mok
% Last Updated: 13-03-2021
function visualizeCrossingPerformance(data)
% ND_Y
figure 
subplot(1,4,1)
plotPositive(data);

% ND_NY
subplot(1,4,2)
plotNegative(data,'ND_NY');

% D_Y
subplot(1,4,3)
plotNegative(data,'D_Y');

% D_NY
subplot(1,4,4)
plotNegative(data,'D_NY');
end

%% Helper functions
function plotPositive(data)
fld_map = fieldnames(data.score.ND_Y);

% find xlim
xdata = zeros(size(fld_map));
for m=1:length(fld_map)
    xdata(m) = data.acpt.(fld_map{m});
end

hold on
for m=1:length(fld_map)
    X = data.acpt.(fld_map{m});
    Y = data.score.ND_Y.(fld_map{m}).mean;
    ystd = data.score.ND_Y.(fld_map{m}).std;
    xstd = 0;
    
    p = errorbar(X,Y,ystd,ystd,xstd,xstd,'o','MarkerSize',10,'CapSize',28);
    c = get(p,'Color');
    p.MarkerFaceColor = c;
    p2 = plot([X-xstd, X+xstd], [Y, Y],'LineWidth',2);
    p2.Color = c;
    set(get(get(p2,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
    p3 = plot([X, X], [Y-ystd, Y+ystd],'LineWidth',2);
    p3.Color = c;
    set(get(get(p3,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
end
legend('Baseline', 'Gaze to Yield', 'Look Away to Yield','Location','southwest');
grid on;
xlabel('Mean acceptance score','FontSize',15,'FontWeight','bold'); xlim([-2 2]); 
ylabel('Mean performance score','FontSize',15,'FontWeight','bold'); ylim([-2 2]); 
title('No distraction - Yielding','FontSize',15,'FontWeight','bold');
ax=gca; ax.FontSize = 20;
xlim([round((min(xdata)-0.2)*5)/5 round((max(xdata)+0.2)*5)/5])
ylim([0 100])
end

function plotNegative(data, condition)
fld_con = fieldnames(data.score);
con = find(strcmp(fld_con,condition));
fld_map = fieldnames(data.score.(fld_con{con}));
titlestr = {'No distraction - Yielding','No distraction - No yielding', 'Distraction - Yielding','Distraction - No yielding'};

% find xlim
xdata = zeros(size(fld_map));
for m=1:length(fld_map)
    xdata(m) = data.acpt.(fld_map{m});
end

hold on
for m=1:length(fld_map)
    X = data.acpt.(fld_map{m});
    Y = data.score.(fld_con{con}).(fld_map{m}).mean;
    ystd = data.score.(fld_con{con}).(fld_map{m}).std;
    xstd = 0;
    
    p = errorbar(X,Y,ystd,ystd,xstd,xstd,'o','MarkerSize',10,'CapSize',28);
    c = get(p,'Color');
    p.MarkerFaceColor = c;
    p2 = plot([X-xstd, X+xstd], [Y, Y],'LineWidth',2);
    p2.Color = c;
    set(get(get(p2,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
    p3 = plot([X, X], [Y-ystd, Y+ystd],'LineWidth',2);
    p3.Color = c;
    set(get(get(p3,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
end
legend('Baseline', 'Gaze to Yield', 'Look Away to Yield','Location','southwest');
grid on;
xlabel('Mean acceptance score','FontSize',15,'FontWeight','bold'); xlim([-2 2]); 
ylabel('Mean performance score','FontSize',15,'FontWeight','bold'); ylim([-2 2]); 
title(titlestr{con},'FontSize',15,'FontWeight','bold');
ax=gca; ax.FontSize = 20;
xlim([round((min(xdata)-0.2)*5)/5 round((max(xdata)+0.2)*5)/5])
ylim([0 100])
end
