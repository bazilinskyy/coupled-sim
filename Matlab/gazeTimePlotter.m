%% GazeTimePlotter
% This script vizualizes the gaze data from the pedestrian and the
% passenger
% Author: Johnson Mok
% Last Updated: 18-01-2021

function gazeTimePlotter(data)
%% EyeContact 
[EC_ND_Y, EC_ND_NY, EC_D_Y, EC_D_NY] = getEyeContact(data);
visEyeContact(EC_ND_Y, EC_ND_NY, EC_D_Y, EC_D_NY);

%% Passenger gaze time
% Full trial
[watch_ND_Y, watch_ND_NY, watch_D_Y, watch_D_NY] = getFullWatch(data);
visFullWatch(watch_ND_Y, watch_ND_NY, watch_D_Y, watch_D_NY);

% Passenger gazing at pedestrian during phases 
[watchP2_ND_Y, watchP2_ND_NY, watchP2_D_Y, watchP2_D_NY] = getOrganizedDY(data, "pa", "time2", "watch");% Phase 2: Tracking till AV stop
[watchP3_ND_Y, watchP3_ND_NY, watchP3_D_Y, watchP3_D_NY] = getOrganizedDY(data, "pa", "time3", "watch");% Phase 3: During yield
[watchP4_ND_Y, watchP4_ND_NY, watchP4_D_Y, watchP4_D_NY] = getOrganizedDY(data, "pa", "time4", "watch");% Phase 4: After reset

% Visualization of passenger gazing at pedestrian during phases 
visPhaseWatch(watchP2_ND_Y,watchP3_ND_Y,watchP4_ND_Y,watchP2_D_Y,watchP3_D_Y,watchP4_D_Y,"yield")
visPhaseWatch(watchP2_ND_NY,watchP3_ND_NY,watchP4_ND_NY,watchP2_D_NY,watchP3_D_NY,watchP4_D_NY,"no")

% Passenger invalid tracking during phases
[invalidP2_ND_Y, invalidP2_ND_NY, invalidP2_D_Y, invalidP2_D_NY] = getOrganizedDY(data, "pa", "time2", "invalid");% Phase 2: Tracking till AV stop
[invalidP3_ND_Y, invalidP3_ND_NY, invalidP3_D_Y, invalidP3_D_NY] = getOrganizedDY(data, "pa", "time3", "invalid");% Phase 3: During yield
[invalidP4_ND_Y, invalidP4_ND_NY, invalidP4_D_Y, invalidP4_D_NY] = getOrganizedDY(data, "pa", "time4", "invalid");% Phase 4: After reset

% Passenger not watching pedestrian during phases
[nwP2_ND_Y, nwP2_ND_NY, nwP2_D_Y, nwP2_D_NY] = getOrganizedDY(data, "pa", "time2", "nowatch");% Phase 2: Tracking till AV stop
[nwP3_ND_Y, nwP3_ND_NY, nwP3_D_Y, nwP3_D_NY] = getOrganizedDY(data, "pa", "time3", "nowatch");% Phase 3: During yield
[nwP4_ND_Y, nwP4_ND_NY, nwP4_D_Y, nwP4_D_NY] = getOrganizedDY(data, "pa", "time4", "nowatch");% Phase 4: After reset

% Calculate mean


end

%% Helper functions
function out = ceilHalf(in)
 out = floor(in) + ceil((in-floor(in))/0.5)*0.5;
end
function out = calcYlim(in1, in2)
max1 = max(in1,[],'all');
max2 = max(in2,[],'all');
largest = max([max1, max2]);
out = ceilHalf(largest);
end
function [EC_ND_Y, EC_ND_NY, EC_D_Y, EC_D_NY] = getEyeContact(data)
% ND_Y: ED 0, 4, 8
EC_ND_Y(:,1) = data.Data_ED_0.HostFixedTimeLog.eyeContact';
EC_ND_Y(:,2) = data.Data_ED_4.HostFixedTimeLog.eyeContact';
EC_ND_Y(:,3) = data.Data_ED_8.HostFixedTimeLog.eyeContact';

% ND_NY: ED 1, 5, 9
EC_ND_NY(:,1) = data.Data_ED_1.HostFixedTimeLog.eyeContact';
EC_ND_NY(:,2) = data.Data_ED_5.HostFixedTimeLog.eyeContact';
EC_ND_NY(:,3) = data.Data_ED_9.HostFixedTimeLog.eyeContact';

% D_Y: ED 2, 6, 10
EC_D_Y(:,1) = data.Data_ED_2.HostFixedTimeLog.eyeContact';
EC_D_Y(:,2) = data.Data_ED_6.HostFixedTimeLog.eyeContact';
EC_D_Y(:,3) = data.Data_ED_10.HostFixedTimeLog.eyeContact';

% D_NY: ED 1, 5, 9
EC_D_NY(:,1) = data.Data_ED_3.HostFixedTimeLog.eyeContact';
EC_D_NY(:,2) = data.Data_ED_7.HostFixedTimeLog.eyeContact';
EC_D_NY(:,3) = data.Data_ED_11.HostFixedTimeLog.eyeContact';
end
function visEyeContact(EC_ND_Y, EC_ND_NY, EC_D_Y, EC_D_NY)
strMap = {'Baseline','Mapping 1','Mapping 2'};
lim1 = calcYlim(EC_ND_Y, EC_D_Y);
lim2 = calcYlim(EC_ND_NY, EC_D_NY);
figure;
subplot(2,2,1)
boxplot(EC_ND_Y,'Labels',strMap);
grid on; ylim([-0.5 lim1]);
ylabel('Eye Contact in [s]'); title('Eye contact - No distraction - Yielding');

subplot(2,2,3)
boxplot(EC_ND_NY,'Labels',strMap);
grid on; ylim([-0.5 lim2]);
ylabel('Eye Contact in [s]'); title('Eye contact - No distraction - No yielding');

subplot(2,2,2)
boxplot(EC_D_Y,'Labels',strMap);
grid on; ylim([-0.5 lim1]);
ylabel('Eye Contact in [s]'); title('Eye contact - Distraction - Yielding');

subplot(2,2,4)
boxplot(EC_D_NY,'Labels',strMap);
grid on; ylim([-0.5 lim2]);
ylabel('Eye Contact in [s]'); title('Eye contact - Distraction - No yielding');
end

function [watch_ND_Y, watch_ND_NY, watch_D_Y, watch_D_NY] = getFullWatch(data)
% ND_Y: ED 0, 4, 8
watch_ND_Y(:,1) = data.Data_ED_0.HostFixedTimeLog.pa.full.watch';
watch_ND_Y(:,2) = data.Data_ED_4.HostFixedTimeLog.pa.full.watch';
watch_ND_Y(:,3) = data.Data_ED_8.HostFixedTimeLog.pa.full.watch';

% ND_NY: ED 1, 5, 9
watch_ND_NY(:,1) = data.Data_ED_1.HostFixedTimeLog.pa.full.watch';
watch_ND_NY(:,2) = data.Data_ED_5.HostFixedTimeLog.pa.full.watch';
watch_ND_NY(:,3) = data.Data_ED_9.HostFixedTimeLog.pa.full.watch';

% D_Y: ED 2, 6, 10
watch_D_Y(:,1) = data.Data_ED_2.HostFixedTimeLog.pa.full.watch';
watch_D_Y(:,2) = data.Data_ED_6.HostFixedTimeLog.pa.full.watch';
watch_D_Y(:,3) = data.Data_ED_10.HostFixedTimeLog.pa.full.watch';

% D_NY: ED 1, 5, 9
watch_D_NY(:,1) = data.Data_ED_3.HostFixedTimeLog.pa.full.watch';
watch_D_NY(:,2) = data.Data_ED_7.HostFixedTimeLog.pa.full.watch';
watch_D_NY(:,3) = data.Data_ED_11.HostFixedTimeLog.pa.full.watch';
end
function visFullWatch(watch_ND_Y, watch_ND_NY, watch_D_Y, watch_D_NY)
strMap = {'Baseline','Mapping 1','Mapping 2'};
titlestr = '[Full run]';

lim1 = calcYlim(watch_ND_Y, watch_D_Y);
lim2 = calcYlim(watch_ND_NY, watch_D_NY);

figure;
subplot(2,2,1)
boxplot(watch_ND_Y,'Labels',strMap);
grid on; ylim([-0.5 lim1]); 
ylabel('Gaze time in [s]'); title(join([titlestr,' Passenger gazing pedestrian - No distraction - Yielding']));

subplot(2,2,3)
boxplot(watch_ND_NY,'Labels',strMap);
grid on; ylim([-0.5 lim2]);
ylabel('Gaze time in [s]'); title(join([titlestr,' Passenger gazing pedestrian - No distraction - No yielding']));

subplot(2,2,2)
boxplot(watch_D_Y,'Labels',strMap);
grid on; ylim([-0.5 lim1]);
ylabel('Gaze time in [s]'); title(join([titlestr,' Passenger gazing pedestrian - Distraction - Yielding']));

subplot(2,2,4)
boxplot(watch_D_NY,'Labels',strMap);
grid on; ylim([-0.5 lim2]);
ylabel('Gaze time in [s]'); title(join([titlestr,' Passenger gazing pedestrian - Distraction - No yielding']));
end

function [watch_ND_Y, watch_ND_NY, watch_D_Y, watch_D_NY] = getOrganizedDY(data, role, phase, type)
field_role = fieldnames(data.Data_ED_0.HostFixedTimeLog);
if(isStringScalar(role))
    role = find(strcmp(field_role, role));
end
field_phase = fieldnames(data.Data_ED_0.HostFixedTimeLog.(field_role{role}));
if(isStringScalar(phase))
    phase = find(strcmp(field_phase, phase));
end
field_type = fieldnames(data.Data_ED_0.HostFixedTimeLog.(field_role{role}).(field_phase{phase}));
if(isStringScalar(type))
    type = find(strcmp(field_type, type));
end

% ND_Y: ED 0, 4, 8
watch_ND_Y(:,1) = data.Data_ED_0.HostFixedTimeLog.(field_role{role}).(field_phase{phase}).(field_type{type})';
watch_ND_Y(:,2) = data.Data_ED_4.HostFixedTimeLog.(field_role{role}).(field_phase{phase}).(field_type{type})';
watch_ND_Y(:,3) = data.Data_ED_8.HostFixedTimeLog.(field_role{role}).(field_phase{phase}).(field_type{type})';

% ND_NY: ED 1, 5, 9
watch_ND_NY(:,1) = data.Data_ED_1.HostFixedTimeLog.(field_role{role}).(field_phase{phase}).(field_type{type})';
watch_ND_NY(:,2) = data.Data_ED_5.HostFixedTimeLog.(field_role{role}).(field_phase{phase}).(field_type{type})';
watch_ND_NY(:,3) = data.Data_ED_9.HostFixedTimeLog.(field_role{role}).(field_phase{phase}).(field_type{type})';

% D_Y: ED 2, 6, 10
watch_D_Y(:,1) = data.Data_ED_2.HostFixedTimeLog.(field_role{role}).(field_phase{phase}).(field_type{type})';
watch_D_Y(:,2) = data.Data_ED_6.HostFixedTimeLog.(field_role{role}).(field_phase{phase}).(field_type{type})';
watch_D_Y(:,3) = data.Data_ED_10.HostFixedTimeLog.(field_role{role}).(field_phase{phase}).(field_type{type})';

% D_NY: ED 1, 5, 9
watch_D_NY(:,1) = data.Data_ED_3.HostFixedTimeLog.(field_role{role}).(field_phase{phase}).(field_type{type})';
watch_D_NY(:,2) = data.Data_ED_7.HostFixedTimeLog.(field_role{role}).(field_phase{phase}).(field_type{type})';
watch_D_NY(:,3) = data.Data_ED_11.HostFixedTimeLog.(field_role{role}).(field_phase{phase}).(field_type{type})';
end
function visPhaseWatch(p2,p3,p4,p22,p33,p44,yield)    
strMap = {'[Baseline] ','[Mapping 1] ','[Mapping 2] '};
titlestr = {' - No distraction - Yielding',' - Distraction - Yielding',' - No distraction - No yielding',' - Distraction - No yielding'};
if(strcmp(yield,"yield"))
	idx = [1,2];
else
    idx = [3,4];
end
tstr1 = join(['Passenger gazing pedestrian', titlestr(idx(1))]);
tstr2 = join(['Passenger gazing pedestrian', titlestr(idx(2))]);
i = [1,3,5];
figure;
for m1 = 1:length(strMap)
    subplot(3,2,i(m1));
    boxplot([p2(:,m1); p3(:,m1); p4(:,m1)],[ones(size(p2(:,m1))); 2*ones(size(p3(:,m1))); 3*ones(size(p4(:,m1)))], ...
        'Labels',{'Tracking till AV yield','During yield','After reset'});
    ylabel('Passenger gazing pedestrian time in [s]'); title(join([strMap(m1),tstr1]));

    subplot(3,2,i(m1)+1);
    boxplot([p22(:,m1); p33(:,m1); p44(:,m1)],[ones(size(p22(:,m1))); 2*ones(size(p33(:,m1))); 3*ones(size(p44(:,m1)))], ...
        'Labels',{'Tracking till AV yield','During yield','After reset'});
    ylabel('Passenger gazing pedestrian time in [s]'); title(join([strMap(m1),tstr2]));
end
end

function visMeanWatchtype()
% Plot bar graphs with P2-P3-P4
% subplot(2,2,3);
% X = categorical({'Passenger','Pedestrian'});
% Y = [male_pa female_pa; male_pe female_pe];
% h = bar(X,Y);
% set(h, {'DisplayName'},{'Male','Female'}')
% title('Gender'); ylabel('Number of participants'); legend();
end
