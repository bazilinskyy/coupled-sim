%% Visualise eye-contact
% Author: Johnson Mok
% Last Updated: 12-04-2021

function visualizeEyeContact(EC, score)
plotScoreVSECAll(EC.MeanStdEyeContact,score.score2);
plotScoreVSEC_pp_All(EC.eyeContactPerPerson,score.buttonPerPersonWithoutStart);
end

%% Helper function
function plotScoreVSEC(EC,score,con)
fld_con = fieldnames(EC);
c = find(strcmp(fld_con,con));
strcon = {'no distraction - yielding','no distraction - non-yielding',...
    'distraction - yielding','distraction - non-yielding'};
strmap = {'Baseline';'Gaze to Yield';'Look Away to Yield'};
colours = [0, 0.4470, 0.7410; 0.8500, 0.3250, 0.0980; 0.9290, 0.6940, 0.1250];
% figure;
hold on;
X = EC.(fld_con{c}).mean;
Y = [score.(fld_con{c}).map0.mean, score.(fld_con{c}).map1.mean, score.(fld_con{c}).map2.mean];
xstd = EC.(fld_con{c}).std;
ystd = [score.(fld_con{c}).map0.std, score.(fld_con{c}).map1.std, score.(fld_con{c}).map2.std];
labels = strmap;
for i=1:length(X)
    p = errorbar(X(i),Y(i),ystd(i),ystd(i),xstd(i),xstd(i),'o','Color',colours(i,:),'MarkerSize',10,'CapSize',28);
    q = get(p,'Color');
    p.MarkerFaceColor = q;
    p2 = plot([X(i)-xstd(i), X(i)+xstd(i)], [Y(i), Y(i)],'LineWidth',2);
    p2.Color = q;
    set(get(get(p2,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
    p3 = plot([X(i), X(i)], [Y(i)-ystd(i), Y(i)+ystd(i)],'LineWidth',2);
    p3.Color = q;
    set(get(get(p3,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
end
legend(labels,'Location','southeast');
xlabel('Mutual gazing time in [s]','FontSize',15,'FontWeight','bold'); 
ylabel('Crossing performance score','FontSize',15,'FontWeight','bold'); 
title(strcon{c},'FontSize',20,'FontWeight','bold')
ax=gca; ax.FontSize = 15;
grid on;
end
function plotScoreVSECAll(EC,score)
fld_con = fieldnames(EC);
figure
for c=1:length(fld_con)
    subplot(1,4,c)
    plotScoreVSEC(EC,score,fld_con{c})
end
end

function plotScoreVSEC_pp(EC,score,con)
fld_con = fieldnames(EC);
c = find(strcmp(fld_con,con));
colours = [0, 0.4470, 0.7410; 0.8500, 0.3250, 0.0980; 0.9290, 0.6940, 0.1250];
strcon = {'no distraction - yielding','no distraction - non-yielding',...
    'distraction - yielding','distraction - non-yielding'};
%
X = EC.(fld_con{c});
Y = [score.(fld_con{c}).map0', score.(fld_con{c}).map1', score.(fld_con{c}).map2'];
%
hold on;
for j=1:3
    P= [X(:,j),Y(:,j)];
    pointsAreCollinear = @(P) rank(P(2:end,:) - P(1,:)) == 1; % Check collinearity
    draw = ~pointsAreCollinear(P);
    if draw == 1
        k = convhull(P);   
        f = fill(P(k,1),P(k,2),colours(j,:),'FaceAlpha',0.5); %plot(P(k,1),P(k,2),'Color',colours(i,:));
        set(get(get(f,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
    end
end
for i=1:3
    scatter(X(:,i),Y(:,i),40,'MarkerEdgeColor','k',...
              'MarkerFaceColor',colours(i,:),...
              'LineWidth',1.5);
end
grid on;
legend({'Baseline';'Gaze to Yield';'Look Away to Yield'},'Location','southeast');
xlabel('Mutual gazing time in [s]','FontSize',15,'FontWeight','bold'); 
ylabel('Crossing performance score','FontSize',15,'FontWeight','bold');
title(strcon{c},'FontSize',15,'FontWeight','bold')
ax=gca; ax.FontSize = 15;
end
function plotScoreVSEC_pp_All(EC,score)
fld_con = fieldnames(EC);
figure
for c=1:length(fld_con)
    subplot(2,2,c)
    plotScoreVSEC_pp(EC,score,fld_con{c})
end
end