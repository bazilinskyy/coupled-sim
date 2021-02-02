%% Visualization of No Category Post Block 
% Author: Johnson Mok
% Last Updated: 02-02-2021

function visualizeNoCategoryPostBlock(NoCat)
%% Passenger
% visualizeNoCatPassenger(NoCat);
% visualizeEasyDirectLaser(NoCat);
% visualizeLaserDistracting(NoCat);
% visualizeVehicleActAsPredicted(NoCat);

figure
subplot(1,2,1);
plotMeanError(NoCat.directLaser_pa,'direct laser','easy to direct',true);
subplot(1,2,2);
plotBar(NoCat.directLaser_val_pa ,NoCat.directLaser_name_pa, NoCat.N, 'Easy to direct laser',true);

figure
subplot(1,2,1);
plotMeanError(NoCat.laserDistract_pa,'laser distracting','laser distracting',true);
subplot(1,2,2);
plotBar(NoCat.laserDistract_val_pa ,NoCat.laserDistract_name_pa, NoCat.N, 'Laser distracting',true);

figure
subplot(1,2,1);
plotMeanError(NoCat.VAP_pa,'VAP','vehicle act as predicted',true);
subplot(1,2,2);
plotBar(NoCat.VAP_val_pa ,NoCat.VAP_name_pa, NoCat.N, 'Vehicle act as predicted',true);

%% Pedestrian
% visualizeNoCatPedestrian(NoCat)
figure
subplot(1,2,1);
plotMeanError(NoCat.prefMapping,'Mappping preference','prefer mapping over the baseline',true);
subplot(1,2,2);
plotBar(NoCat.prefMapping_val ,NoCat.prefMapping_name, NoCat.N, 'Prefer mapping over the baseline',true);

figure
subplot(1,2,1);
plotMeanError(NoCat.clearVehicleYield,'clear vehicle yield','clear when the vehicle yields',false);
subplot(1,2,2);
plotBar(NoCat.clearVehicleYield_val ,NoCat.clearVehicleYield_name, NoCat.N, 'Clear when the vehicle yields',false);


end

%% Helper functions
function plotMeanError(data, titlestr, LikertfactorStr, skipBaseline)
% Data prep
strmap = {'Baseline','Mapping 1','Mapping 2'};
mean = data.mean;
err = data.std;
if (skipBaseline == true)
    strmap = strmap(2:end);
    mean = mean(2:end);
    err = err(2:end);
end
x = categorical(strmap);
x = reordercats(x,strmap);
titstr = join(['Mean ', titlestr ,' score per mapping']);
colourcodes = [0, 0.4470, 0.7410; 0.8500, 0.3250, 0.0980; 0.9290, 0.6940, 0.1250];
% Bar graph
for i = 1:length(x)
h = bar(x(i),mean(i));
set(h, 'FaceColor', colourcodes(i,:));
a = get(gca,'XTickLabel');
set(gca,'XTickLabel',a,'FontSize',18,'FontWeight','bold');
ylabel({join(['Mean ',titlestr,' score']);LikertfactorStr},'FontSize',18,'FontWeight','bold');
ylim([0 7]);
title(titstr,'FontSize',18,'FontWeight','bold');
hold on;
grid on;
end
% Mean line
plot(x,mean,'LineWidth',2);
% Errorbar
er = errorbar(x,mean,err,'CapSize',20);
er.Color = [0 0 0];
er.LineStyle = 'none';
hold off;
end
function plotBar(data ,name, N, titlestr, skipBaseline)
strmap = {'Baseline','Mapping 1','Mapping 2'};
if (skipBaseline == true)
    strmap = strmap(2:end);
end
X = categorical(name);
X = reordercats(X,name);
if (skipBaseline == false)
    h = barh(X,[data(1,:); data(2,:); data(3,:)]);
elseif (skipBaseline == true)
    h = barh(X,[data(2,:); data(3,:)]);
end
set(h, {'DisplayName'},strmap')
a = get(gca,'YTickLabel');
set(gca,'YTickLabel',a,'FontSize',18,'FontWeight','bold');
title(join([titlestr, ' Score Frequency']),'FontSize',18,'FontWeight','bold'); 
xlabel({'Percentage of participants in [%]';['N = ', num2str(N)]},'FoNtSize',18,'FontWeight','bold'); 
xlim([0 100]);
legend(); grid on;
end

function visualizeEasyDirectLaser(NoCat)
strMap = {'baseline','mapping 1','mapping 2'};
figure;
subplot(2,1,1);
boxplot(NoCat.directLaser_pa.all,'Labels',strMap);
ylabel('Score'); title('Easy to direct the eye-gaze visualization.');

subplot(2,1,2);
X = categorical(NoCat.directLaser_name_pa);
X = reordercats(X,NoCat.directLaser_name_pa);
h = bar(X,NoCat.directLaser_val_pa);
set(h, {'DisplayName'},strMap')
title('Easy to direct the eye-gaze visualization.'); ylabel('Number of participants'); legend();
end
function visualizeLaserDistracting(NoCat)
figure;
subplot(2,1,1);
boxplot(NoCat.laserDistract_pa.all,'Labels',{'baseline','mapping 1','mapping 2'});
ylabel('Score'); title('Eye-gaze visualization distracting.');
subplot(2,1,2);
X = categorical(NoCat.laserDistract_name_pa);
X = reordercats(X,NoCat.laserDistract_name_pa);
h = bar(X,NoCat.laserDistract_val_pa);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('Eye-gaze visualization distracting.'); ylabel('Number of participants'); legend();
end
function visualizeVehicleActAsPredicted(NoCat)
figure;
subplot(2,1,1);
boxplot(NoCat.VAP_pa.all,'Labels',{'baseline','mapping 1','mapping 2'});
ylabel('Score'); title('The vehicle acted as predicted');
subplot(2,1,2);
X = categorical(NoCat.VAP_name_pa);
X = reordercats(X,NoCat.VAP_name_pa);
h = bar(X,NoCat.VAP_val_pa);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('The vehicle acted as predicted.'); ylabel('Number of participants'); legend();
end
function visualizeNoCatPassenger(NoCat)
figure
% Easy to direct the eye-gaze visualization
subplot(2,3,1);
boxplot(NoCat.directLaser_pa.all,'Labels',{'baseline','mapping 1','mapping 2'});
ylabel('Score'); title('Easy to direct the eye-gaze visualization.');
subplot(2,3,4);
X = categorical(NoCat.directLaser_name_pa);
X = reordercats(X,NoCat.directLaser_name_pa);
h = bar(X,NoCat.directLaser_val_pa);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('Easy to direct the eye-gaze visualization.'); ylabel('Number of participants'); legend();

% Eye-gaze visualization distracting
subplot(2,3,2);
boxplot(NoCat.laserDistract_pa.all,'Labels',{'baseline','mapping 1','mapping 2'});
ylabel('Score'); title('Eye-gaze visualization distracting.');
subplot(2,3,5);
X = categorical(NoCat.laserDistract_name_pa);
X = reordercats(X,NoCat.laserDistract_name_pa);
h = bar(X,NoCat.laserDistract_val_pa);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('Eye-gaze visualization distracting.'); ylabel('Number of participants'); legend();

% Vehicle acted as predicted 
subplot(2,3,3);
boxplot(NoCat.VAP_pa.all,'Labels',{'baseline','mapping 1','mapping 2'});
ylabel('Score'); title('The vehicle acted as predicted');
subplot(2,3,6);
X = categorical(NoCat.VAP_name_pa);
X = reordercats(X,NoCat.VAP_name_pa);
h = bar(X,NoCat.VAP_val_pa);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('The vehicle acted as predicted.'); ylabel('Number of participants'); legend();
end

function visualizeNoCatPedestrian(NoCat)
figure
% Prefer over baseline
subplot(2,2,1);
boxplot(NoCat.prefMapping.all,'Labels',{'baseline','mapping 1','mapping 2'});
ylabel('Score'); title('You prefer the mapping over the baseline.');
subplot(2,2,3);
X = categorical(NoCat.prefMapping_name);
X = reordercats(X,NoCat.prefMapping_name);
h = bar(X,NoCat.prefMapping_val);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('You prefer the mapping over the baseline.'); ylabel('Number of participants'); legend();
% Clear vehicle yield
subplot(2,2,2);
boxplot(NoCat.clearVehicleYield.all,'Labels',{'baseline','mapping 1','mapping 2'});
ylabel('Score'); title('It was clear to you when the vehicle was going to yield.');
subplot(2,2,4);
X = categorical(NoCat.clearVehicleYield_name);
X = reordercats(X,NoCat.clearVehicleYield_name);
h = bar(X,NoCat.clearVehicleYield_val);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('It was clear to you when the vehicle was going to yield.'); ylabel('Number of participants'); legend();
end



