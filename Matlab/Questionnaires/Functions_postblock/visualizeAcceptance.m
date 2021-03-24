%% Visualization Acceptance Van Der Laan
% Author: Johnson Mok
% Last Updated: 27-01-2021

% This script visualizes the Acceptance data in the form of tables and
% figures. 
% - Statistical significance using repeated measures ANOVA
% - Reliability analysis using Cronbach's alpha
% - Acceptance opinion change using the difference in scores between
% mappings.

function visualizeAcceptance(acpt_pe, acpt_pa)
%% Plots
% plotAcceptance(acpt_pe, acpt_pa);
% plotAcceptanceBoxpot(acpt_pe, acpt_pa);
% plotSubAcceptanceBar(acpt_pe, acpt_pa);
% plotUsefulnessVSsatisfying(acpt_pe, acpt_pa);
% plotUsefulnessVSsatisfyingErrorbar(acpt_pe, acpt_pa);

figure;
subplot(1,2,1);
plotUsefulnessVSsatisfyingErrorbarSingle(acpt_pa,'Passenger');
subplot(1,2,2);
plotUsefulnessVSsatisfyingErrorbarSingle(acpt_pe,'Pedestrian');

%% Tables
tableCron(acpt_pe, acpt_pa);
tableMeanAllScoresAcceptance(acpt_pe, acpt_pa)
tableTestStatistic(acpt_pa, 'Passenger');
tableTestStatistic(acpt_pe, 'Pedestrian');
tableFvalues(acpt_pe, acpt_pa, 5.849, 0.001); % input: critical F value, alpha

end

%% Helper functions - Plots
function plotAcceptance(acpt_pe, acpt_pa)
figure
% acceptance - passenger
subplot(1,2,1);
X = categorical(acpt_pa.Laan0.name);
X = reordercats(X,acpt_pa.Laan0.name);
h = plot(X,[acpt_pa.Laan0.meanAcc; acpt_pa.Laan1.meanAcc; acpt_pa.Laan2.meanAcc]);
y0 = yline(acpt_pa.Laan0.meanmean,'--','Baseline','LineWidth',2); y0.Color = [0, 0.4470, 0.7410]; 
y1 = yline(acpt_pa.Laan1.meanmean,'--','Mapping 1','LineWidth',2); y1.Color = [0.8500, 0.3250, 0.0980];
y2 = yline(acpt_pa.Laan2.meanmean,'--','Mapping 2','LineWidth',2); y2.Color = [0.9290, 0.6940, 0.1250];
grid on;
ylim([-2 2]);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
set(get(get(y0,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
set(get(get(y1,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
set(get(get(y2,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
title('Acceptance score - Passenger'); ylabel('Score'); legend();
% acceptance - pedestrian
subplot(1,2,2);
X = categorical(acpt_pe.Laan0.name);
X = reordercats(X,acpt_pe.Laan0.name);
h = plot(X,[acpt_pe.Laan0.meanAcc; acpt_pe.Laan1.meanAcc; acpt_pe.Laan2.meanAcc]);
y0 = yline(acpt_pe.Laan0.meanmean,'--','Baseline','LineWidth',2); y0.Color = [0, 0.4470, 0.7410];
y1 = yline(acpt_pe.Laan1.meanmean,'--','Mapping 1','LineWidth',2); y1.Color = [0.8500, 0.3250, 0.0980];
y2 = yline(acpt_pe.Laan2.meanmean,'--','Mapping 2','LineWidth',2); y2.Color = [0.9290, 0.6940, 0.1250];
grid on;
ylim([-2 2]);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
set(get(get(y0,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
set(get(get(y1,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
set(get(get(y2,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('Acceptance score - Pedestrian'); ylabel('Score'); legend();
end
function plotAcceptanceBoxpot(acpt_pe, acpt_pa)
figure
% Boxplot acceptance items passenger
subplot(3,2,1);
boxplot(acpt_pa.acc_0,'Labels',acpt_pa.Laan0.name);
ylabel('Score'); title('Acceptance score Baseline - Passenger.');
ylim([-2.5 2.5]);
subplot(3,2,3);
boxplot(acpt_pa.acc_1,'Labels',acpt_pa.Laan1.name);
ylabel('Score'); title('Acceptance score Mapping 1 - Passenger.');
ylim([-2.5 2.5]);
subplot(3,2,5);
boxplot(acpt_pa.acc_2,'Labels',acpt_pa.Laan2.name);
ylabel('Score'); title('Acceptance score Mapping 2 - Passenger.');
ylim([-2.5 2.5]);
% Boxplot acceptance items pedestrian
subplot(3,2,2);
boxplot(acpt_pe.acc_0,'Labels',acpt_pe.Laan0.name);
ylabel('Score'); title('Acceptance score Baseline - Pedestrian.');
ylim([-2.5 2.5]);
subplot(3,2,4);
boxplot(acpt_pe.acc_1,'Labels',acpt_pe.Laan1.name);
ylabel('Score'); title('Acceptance score Mapping 1 - Pedestrian.');
ylim([-2.5 2.5]);
subplot(3,2,6);
boxplot(acpt_pe.acc_2,'Labels',acpt_pe.Laan2.name);
ylabel('Score'); title('Acceptance score Mapping 2 - Pedestrian.');
ylim([-2.5 2.5]);
end
function plotSubAcceptanceBar(acpt_pe, acpt_pa)
figure
% Usefulness score 
subplot(2,1,1);
X = categorical({'Passenger','Pedestrian'});
X = reordercats(X,{'Passenger','Pedestrian'});
h = bar(X,[acpt_pa.all.U0 acpt_pe.all.U0;...
            acpt_pa.all.U1 acpt_pe.all.U1;...
            acpt_pa.all.U2 acpt_pe.all.U2]);
grid on;
ylim([-2 2]);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('Usefulness'); ylabel('Score'); legend();
% satisfying score 
subplot(2,1,2);
X = categorical({'Passenger','Pedestrian'});
X = reordercats(X,{'Passenger','Pedestrian'});
h = bar(X,[acpt_pa.all.S0 acpt_pe.all.S0;...
            acpt_pa.all.S1 acpt_pe.all.S1;...
            acpt_pa.all.S2 acpt_pe.all.S2]);
grid on;
ylim([-2 2]);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('Satisfying'); ylabel('Score'); legend();
end
function plotUsefulnessVSsatisfying(acpt_pe, acpt_pa)
% Plot with usefulness against satisfying
figure;
hold on;
X = [acpt_pa.all.S0, acpt_pa.all.S1, acpt_pa.all.S2, acpt_pe.all.S0, acpt_pe.all.S1, acpt_pe.all.S2];
Y = [acpt_pa.all.U0, acpt_pa.all.U1, acpt_pa.all.U2, acpt_pe.all.U0, acpt_pe.all.U1, acpt_pe.all.U2];
labels = {'Passenger - baseline','Passenger - mapping 1','Passenger - mapping 2','Pedestrian - baseline','Pedestrian - mapping 1','Pedestrian - mapping 2'};
for i=1:length(X)
    p = plot(X(i),Y(i),'o','MarkerSize',10);
    c = get(p,'Color');
    p.MarkerFaceColor = c;
end
text(X,Y,labels,'FontSize',14,'VerticalAlignment','bottom','HorizontalAlignment','right')
xlabel('Satisfying','FontSize',15,'FontWeight','bold'); xlim([-2 2]); xline(0,'--');
ylabel('Usefulness','FontSize',15,'FontWeight','bold'); ylim([-2 2]); yline(0,'--');
ax=gca; ax.FontSize = 20;
grid on;
end
function plotUsefulnessVSsatisfyingErrorbar(acpt_pe, acpt_pa)
figure;
hold on;
X = [acpt_pa.all.S0, acpt_pa.all.S1, acpt_pa.all.S2, acpt_pe.all.S0, acpt_pe.all.S1, acpt_pe.all.S2];
Y = [acpt_pa.all.U0, acpt_pa.all.U1, acpt_pa.all.U2, acpt_pe.all.U0, acpt_pe.all.U1, acpt_pe.all.U2];
xstd = [acpt_pa.all.stdS0, acpt_pa.all.stdS1, acpt_pa.all.stdS2, acpt_pe.all.stdS0, acpt_pe.all.stdS1, acpt_pe.all.stdS2];
ystd = [acpt_pa.all.stdU0, acpt_pa.all.stdU1, acpt_pa.all.stdU2, acpt_pe.all.stdU0, acpt_pe.all.stdU1, acpt_pe.all.stdU2];
labels = {'Passenger - baseline','Passenger - mapping 1','Passenger - mapping 2','Pedestrian - baseline','Pedestrian - mapping 1','Pedestrian - mapping 2'};
for i=1:length(X)
    p = errorbar(X(i),Y(i),ystd(i),ystd(i),xstd(i),xstd(i),'o','MarkerSize',10,'CapSize',28);
    c = get(p,'Color');
    p.MarkerFaceColor = c;
    p2 = plot([X(i)-xstd(i), X(i)+xstd(i)], [Y(i), Y(i)],'LineWidth',2);
    p2.Color = c;
    set(get(get(p2,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
    p3 = plot([X(i), X(i)], [Y(i)-ystd(i), Y(i)+ystd(i)],'LineWidth',2);
    p3.Color = c;
    set(get(get(p3,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
end
legend(labels,'Location','southeast');
xlabel('Satisfying','FontSize',15,'FontWeight','bold'); xlim([-2 2]); xL = xline(0,'--');
ylabel('Usefulness','FontSize',15,'FontWeight','bold'); ylim([-2 2]); yL = yline(0,'--');
title('Acceptance subscale scores','FontSize',15,'FontWeight','bold')
set(get(get(xL,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
set(get(get(yL,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
ax=gca; ax.FontSize = 20;
grid on;
end
function plotUsefulnessVSsatisfyingErrorbarSingle(acpt,role)
strmap = {'Baseline';'Gaze to Yield';'Look Away to Yield'};
colours = [0, 0.4470, 0.7410; 0.8500, 0.3250, 0.0980; 0.9290, 0.6940, 0.1250];
strRole = join(['[',role,'] - ']);
% figure;
hold on;
X = [acpt.all.S0, acpt.all.S1, acpt.all.S2];
Y = [acpt.all.U0, acpt.all.U1, acpt.all.U2];
xstd = [acpt.all.stdS0, acpt.all.stdS1, acpt.all.stdS2];
ystd = [acpt.all.stdU0, acpt.all.stdU1, acpt.all.stdU2];
labels = strmap;
for i=1:length(X)
    p = errorbar(X(i),Y(i),ystd(i),ystd(i),xstd(i),xstd(i),'o','Color',colours(i,:),'MarkerSize',10,'CapSize',28);
    c = get(p,'Color');
    p.MarkerFaceColor = c;
    p2 = plot([X(i)-xstd(i), X(i)+xstd(i)], [Y(i), Y(i)],'LineWidth',2);
    p2.Color = c;
    set(get(get(p2,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
    p3 = plot([X(i), X(i)], [Y(i)-ystd(i), Y(i)+ystd(i)],'LineWidth',2);
    p3.Color = c;
    set(get(get(p3,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
end
legend(labels,'Location','southeast');
xlabel('Satisfying','FontSize',15,'FontWeight','bold'); xlim([-2 2]); xL = xline(0,'--');
ylabel('Usefulness','FontSize',15,'FontWeight','bold'); ylim([-2 2]); yL = yline(0,'--');
title(join([strRole,'Acceptance subscale scores']),'FontSize',15,'FontWeight','bold')
set(get(get(xL,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
set(get(get(yL,'Annotation'),'LegendInformation'),'IconDisplayStyle','off');
ax=gca; ax.FontSize = 20;
grid on;
end
%% Helper functions - Tables
function tableCron(acpt_pe, acpt_pa)
T_Cron = table(round([acpt_pa.cron.U0; acpt_pa.cron.S0; acpt_pe.cron.U0; acpt_pe.cron.S0],2),... 
               round([acpt_pa.cron.U1; acpt_pa.cron.S1; acpt_pe.cron.U1; acpt_pe.cron.S1],2),...
               round([acpt_pa.cron.U2; acpt_pa.cron.S2; acpt_pe.cron.U2; acpt_pe.cron.S2],2),...
                'VariableNames',{'Baseline' 'Mapping 1' 'Mapping 2'},...
                'RowNames',{'Passenger - Usefulness' 'Passenger - Satisfying' 'Pedestrian - Usefulness' 'Pedestrian - Satisfying'});
fprintf('<strong>Cronbachs alpha table:</strong>\n');
disp(T_Cron);
disp('Note: According to Van Der Laan, a Cronbachs alpha above 0.65 is needed to deem the reliability of the subscale scores sufficiently high.');
end
function tableMeanAllScoresAcceptance(acpt_pe, acpt_pa)
T_acPa = table(round(acpt_pa.Laan0.meanAcc,3)',round(acpt_pa.Laan1.meanAcc,3)',round(acpt_pa.Laan2.meanAcc,3)',...
               round(acpt_pe.Laan0.meanAcc,3)',round(acpt_pe.Laan1.meanAcc,3)',round(acpt_pe.Laan2.meanAcc,3)',...
               'VariableNames',{'Passenger - Baseline' 'Passenger - Mapping 1' 'Passenger - Mapping 2' 'Pedestrian - Baseline' 'Pedestrian - Mapping 1' 'Pedestrian - Mapping 2'},...
               'RowNames',acpt_pa.Laan0.name);
fprintf('<strong>Mean acceptance score for all acceptance questions table:</strong>\n');
disp(T_acPa);
end
function tableTestStatistic(acpt,role)
colName = {'SS','df','MS','F'};
rowName = {'Between','Within','-Subjects','-Error','Total'};
titlestr = {'Usefulness','Satisfying'};
fieldT = fieldnames(acpt.ANOVA);
    idxTU = find(strcmp(fieldT,'TU'));
    idxTS = find(strcmp(fieldT,'TS'));
j = 0;
for i = [idxTU, idxTS]
j = j+1;
colSS = [acpt.ANOVA.(fieldT{i}).SS_b; acpt.ANOVA.(fieldT{i}).SS_w; acpt.ANOVA.(fieldT{i}).SS_s; acpt.ANOVA.(fieldT{i}).SS_e; acpt.ANOVA.(fieldT{i}).SS_t];
coldf = [acpt.ANOVA.(fieldT{i}).df_b; acpt.ANOVA.(fieldT{i}).df_w; acpt.ANOVA.(fieldT{i}).df_s; acpt.ANOVA.(fieldT{i}).df_e; acpt.ANOVA.(fieldT{i}).df_t];
colMS = [acpt.ANOVA.(fieldT{i}).MS_b; nan; nan; acpt.ANOVA.(fieldT{i}).MS_e; nan];
colF  = [acpt.ANOVA.(fieldT{i-1}); nan; nan; nan; nan];
T_testStat = table(colSS,coldf,colMS,colF,...
    'VariableNames',colName,'RowNames',rowName);
disp(join(['[',role,' - ',titlestr{j},'] Test statistics table:']));
disp(T_testStat);
end
end
function tableFvalues(acpt_pe, acpt_pa, critF, alpha)
colName = {'F','Hypothesis'};
rowName = {'Passenger - Usefulness','Passenger - Satisfying',...
    'Pedestrian - Usefulness','Pedestrian - Satisfying'};
colF = [acpt_pa.ANOVA.FU; acpt_pa.ANOVA.FS; acpt_pe.ANOVA.FU; acpt_pe.ANOVA.FS];
colH = cell(size(colF));
for i = 1:length(colF)
    if(colF(i)>critF)
        colH{i,:} = 'Reject';
    else
        colH{i,:} = 'Accept';
    end
end
T_Fval = table(colF,colH,...
    'VariableNames',colName,'RowNames',rowName);
disp(['If F is greater than ',num2str(critF),' (alpha <',num2str(alpha),'), reject the null hypothesis:']);
disp(T_Fval);
end




