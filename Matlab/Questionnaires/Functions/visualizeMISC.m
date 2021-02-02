%% Visualization MISC
% Author: Johnson Mok
% Last Updated: 01-02-2021

% This script visualizes the MISC data

function visualizeMISC(misc_pa, misc_pe)
plotMISCbar(misc_pa.name, misc_pa.val, misc_pe.val);

figure
subplot(1,2,1)
plotMISCMeanError(misc_pa.map0.mean, misc_pa.map1.mean, misc_pa.map2.mean, misc_pa.map0.std, misc_pa.map1.std, misc_pa.map2.std, 'Passenger')
yl = get(gca,'ylim');
subplot(1,2,2)
plotMISCMeanError(misc_pe.map0.mean, misc_pe.map1.mean, misc_pe.map2.mean, misc_pe.map0.std, misc_pe.map1.std, misc_pe.map2.std, 'Pedestrian')
ylim(yl);
end

%% Helper functions
function plotMISCbar(MISC_name, MISC_pa, MISC_pe)
% Prep name
name = cell(size(MISC_name));
for i = 1:length(MISC_name)
    ss = strread(MISC_name{i},'%s','delimiter',',') ;
    name{i} = ss{1};
end
% Plot
figure;
X = categorical(name);
X = reordercats(X,name);
h = barh(X,[MISC_pa; MISC_pe]);
set(h, {'DisplayName'},{'passenger','pedestrian',}')
a = get(gca,'YTickLabel');
set(gca,'YTickLabel',a,'FontSize',18,'FontWeight','bold');
title('MISC Score Frequency','FontSize',18,'FontWeight','bold'); 
xlabel('Number of participants','FontSize',18,'FontWeight','bold'); 
legend(); grid on;
end
function plotMISCMeanError(mean0, mean1, mean2, std0, std1, std2, role)
% Data prep
strmap = {'Baseline','Mapping 1','Mapping 2'};
data = [mean0, mean1, mean2];
err = [std0, std1, std2];
x = categorical(strmap);
x = reordercats(x,strmap);
titstr = join(['[',role,'] - Mean MISC score per mapping']);
colourcodes = [0, 0.4470, 0.7410; 0.8500, 0.3250, 0.0980; 0.9290, 0.6940, 0.1250];
% Bar graph
for i = 1:length(x)
h = bar(x(i),data(i));
set(h, 'FaceColor', colourcodes(i,:));
a = get(gca,'XTickLabel');
set(gca,'XTickLabel',a,'FontSize',18,'FontWeight','bold');
ylabel('Mean MISC score','FontSize',18,'FontWeight','bold');
title(titstr,'FontSize',18,'FontWeight','bold');
hold on;
grid on;
end
% Mean line
plot(x,data,'LineWidth',2);
% Errorbar
er = errorbar(x,data,err,'CapSize',20);
er.Color = [0 0 0];
er.LineStyle = 'none';
hold off;
end