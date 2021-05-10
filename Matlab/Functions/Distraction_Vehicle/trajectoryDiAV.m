%% Visualize trajectory distraction AV
% Author: Johnson Mok

function trajectoryDiAV(PreDataV3)
%% Baseline
dataAV_Y = PreDataV3.Data_ED_2.HostFixedTimeLog.participant_1.trial_5.pa.pos.z;
dataDi_Y = PreDataV3.Data_ED_2.HostFixedTimeLog.participant_1.trial_5.diAV.pos.z;
dataAV_NY = PreDataV3.Data_ED_3.HostFixedTimeLog.participant_1.trial_3.pa.pos.z;
dataDi_NY = PreDataV3.Data_ED_3.HostFixedTimeLog.participant_1.trial_3.diAV.pos.z;

figure;
subplot(1,2,1)
visualize(dataAV_Y,dataDi_Y, '[Baseline] ');
subplot(1,2,2)
visualize(dataAV_NY,dataDi_NY, '[Baseline] ');

%% Gaze to yield
dataAV_Y = PreDataV3.Data_ED_6.HostFixedTimeLog.participant_1.trial_5.pa.pos.z;
dataDi_Y = PreDataV3.Data_ED_6.HostFixedTimeLog.participant_1.trial_5.diAV.pos.z;
dataAV_NY = PreDataV3.Data_ED_7.HostFixedTimeLog.participant_1.trial_4.pa.pos.z;
dataDi_NY = PreDataV3.Data_ED_7.HostFixedTimeLog.participant_1.trial_4.diAV.pos.z;

figure;
subplot(1,2,1)
visualize(dataAV_Y,dataDi_Y, '[Gaze to yield] ');
subplot(1,2,2)
visualize(dataAV_NY,dataDi_NY, '[Gaze to yield] ');

%% Look away to yield
dataAV_Y = PreDataV3.Data_ED_10.HostFixedTimeLog.participant_1.trial_7.pa.pos.z;
dataDi_Y = PreDataV3.Data_ED_10.HostFixedTimeLog.participant_1.trial_7.diAV.pos.z;
dataAV_NY = PreDataV3.Data_ED_11.HostFixedTimeLog.participant_1.trial_8.pa.pos.z;
dataDi_NY = PreDataV3.Data_ED_11.HostFixedTimeLog.participant_1.trial_8.diAV.pos.z;

figure;
subplot(1,2,1)
visualize(dataAV_Y,dataDi_Y, '[Look away to yield] ');
subplot(1,2,2)
visualize(dataAV_NY,dataDi_NY, '[Look away to yield] ');

end
%% helper functions
function visualize(AV,DI,map)
pasAV = find(AV<17.19,1,'first')*0.0167;
pasDi = find(DI>17.19,1,'first')*0.0167;
diff = abs(pasAV-pasDi);
x=(0:length(AV)-1)*0.0167;

% figure;
hold on;
plot(x, AV,'LineWidth',2);
plot(x, DI,'LineWidth',2);
yline(17.19,'--','pedestrian','FontSize',15);
xline(pasAV,'--','AV passes pedestrian','LabelVerticalAlignment','bottom','FontSize',15);
xline(pasDi,'--','Distraction vehicle passes pedestrian','LabelVerticalAlignment','bottom','FontSize',15);
title({[map, 'time difference in']; ['reaching pedestrian: ', num2str(diff),' [s]']},'FontSize',15,'FontWeight','bold');
legend('AV','Di');
xlabel('time in [s]','FontSize',15,'FontWeight','bold');
ylabel('z-position value in world coordinate in [m]','FontSize',15,'FontWeight','bold');
grid on;
set(gca,'FontSize',15);


end

