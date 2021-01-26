%% GazeTimePlotter
% This script vizualizes the gaze data from the pedestrian and the
% passenger
% Author: Johnson Mok
% Last Updated: 18-01-2021

function gazeTimePlotter(data, pa_dis, pe_dis)
%% EyeContact 
[EC_ND_Y, EC_ND_NY, EC_D_Y, EC_D_NY] = getEyeContact(data);
visEyeContact(EC_ND_Y, EC_ND_NY, EC_D_Y, EC_D_NY);

%% Passenger gaze time
% Full trial
[watch_ND_Y, watch_ND_NY, watch_D_Y, watch_D_NY] = getFullWatch(data);
visFullWatch(watch_ND_Y, watch_ND_NY, watch_D_Y, watch_D_NY);

%% Passenger gazing at pedestrian during phases 
[watchP2_ND_Y, watchP2_ND_NY, watchP2_D_Y, watchP2_D_NY] = getOrganizedDY(data, "pa", "time2", "watch");% Phase 2: Tracking till AV stop
[watchP3_ND_Y, watchP3_ND_NY, watchP3_D_Y, watchP3_D_NY] = getOrganizedDY(data, "pa", "time3", "watch");% Phase 3: During yield
[watchP4_ND_Y, watchP4_ND_NY, watchP4_D_Y, watchP4_D_NY] = getOrganizedDY(data, "pa", "time4", "watch");% Phase 4: After reset

% Visualization of passenger gazing at pedestrian during phases 
visPhaseWatch(watchP2_ND_Y,watchP3_ND_Y,watchP4_ND_Y,watchP2_D_Y,watchP3_D_Y,watchP4_D_Y,"yield")
visPhaseWatch(watchP2_ND_NY,watchP3_ND_NY,watchP4_ND_NY,watchP2_D_NY,watchP3_D_NY,watchP4_D_NY,"no")

%% Passenger invalid tracking during phases
[invalidP2_ND_Y, invalidP2_ND_NY, invalidP2_D_Y, invalidP2_D_NY] = getOrganizedDY(data, "pa", "time2", "invalid");% Phase 2: Tracking till AV stop
[invalidP3_ND_Y, invalidP3_ND_NY, invalidP3_D_Y, invalidP3_D_NY] = getOrganizedDY(data, "pa", "time3", "invalid");% Phase 3: During yield
[invalidP4_ND_Y, invalidP4_ND_NY, invalidP4_D_Y, invalidP4_D_NY] = getOrganizedDY(data, "pa", "time4", "invalid");% Phase 4: After reset

% Passenger not watching pedestrian during phases
[nwP2_ND_Y, nwP2_ND_NY, nwP2_D_Y, nwP2_D_NY] = getOrganizedDY(data, "pa", "time2", "nowatch");% Phase 2: Tracking till AV stop
[nwP3_ND_Y, nwP3_ND_NY, nwP3_D_Y, nwP3_D_NY] = getOrganizedDY(data, "pa", "time3", "nowatch");% Phase 3: During yield
[nwP4_ND_Y, nwP4_ND_NY, nwP4_D_Y, nwP4_D_NY] = getOrganizedDY(data, "pa", "time4", "nowatch");% Phase 4: After reset

% Calculate mean
meanwatchP2 = mean([watchP2_ND_Y, watchP2_ND_NY, watchP2_D_Y, watchP2_D_NY]);
meanwatchP3 = mean([watchP3_ND_Y, watchP3_ND_NY, watchP3_D_Y, watchP3_D_NY]);
meanwatchP4 = mean([watchP4_ND_Y, watchP4_ND_NY, watchP4_D_Y, watchP4_D_NY]);

meaninvalidP2 = mean([invalidP2_ND_Y, invalidP2_ND_NY, invalidP2_D_Y, invalidP2_D_NY]);
meaninvalidP3 = mean([invalidP3_ND_Y, invalidP3_ND_NY, invalidP3_D_Y, invalidP3_D_NY]);
meaninvalidP4 = mean([invalidP4_ND_Y, invalidP4_ND_NY, invalidP4_D_Y, invalidP4_D_NY]);

meanNWP2 = mean([nwP2_ND_Y, nwP2_ND_NY, nwP2_D_Y, nwP2_D_NY]);
meanNWP3 = mean([nwP3_ND_Y, nwP3_ND_NY, nwP3_D_Y, nwP3_D_NY]);
meanNWP4 = mean([nwP4_ND_Y, nwP4_ND_NY, nwP4_D_Y, nwP4_D_NY]);

% Bar visualization of mean
visMeanWatchtype(meanwatchP2, meanwatchP3, meanwatchP4, meaninvalidP2, meaninvalidP3, meaninvalidP4, meanNWP2, meanNWP3, meanNWP4);

%% Number of people watching at distance z. Divided by phases
[pa_dis_ND_Y, pa_dis_ND_NY, pa_dis_D_Y, pa_dis_D_NY] = getOrganizedDis(pa_dis);
[pe_dis_ND_Y, pe_dis_ND_NY, pe_dis_D_Y, pe_dis_D_NY] = getOrganizedDis(pe_dis);

[pa_valND_Y, pa_nameND_Y] = sumDis(pa_dis_ND_Y);
[pa_valND_NY, pa_nameND_NY] = sumDis(pa_dis_ND_NY);
[pa_valD_Y, pa_nameD_Y] = sumDis(pa_dis_D_Y);
[pa_valD_NY, pa_nameD_NY] = sumDis(pa_dis_D_NY);

[pe_valND_Y, pe_nameND_Y] = sumDis(pe_dis_ND_Y);
[pe_valND_NY, pe_nameND_NY] = sumDis(pe_dis_ND_NY);
[pe_valD_Y, pe_nameD_Y] = sumDis(pe_dis_D_Y);
[pe_valD_NY, pe_nameD_NY] = sumDis(pe_dis_D_NY);

% Visualization
visPasGazePed(pa_valND_Y, pa_nameND_Y, 'pa', 'ND_Y', pa_valD_Y, pa_nameD_Y, 'pa', 'D_Y');
visPasGazePed(pa_valND_NY, pa_nameND_NY, 'pa', 'ND_NY', pa_valD_NY, pa_nameD_NY, 'pa', 'D_NY');
visPasGazePed(pe_valND_Y, pe_nameND_Y, 'pe', 'ND_Y', pe_valD_Y, pe_nameD_Y, 'pe', 'D_Y');
visPasGazePed(pe_valND_NY, pe_nameND_NY, 'pe', 'ND_NY', pe_valD_NY, pe_nameD_NY, 'pe', 'D_NY');

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

function visMeanWatchtype(meanwatchP2, meanwatchP3, meanwatchP4, meaninvalidP2, meaninvalidP3, meaninvalidP4, meanNWP2, meanNWP3, meanNWP4)
% Plot bar graphs with P2-P3-P4
% ND_Y, ND_NY, D_Y, D_NY]
cats = {'Till AV yield','During yield','After yield'};
strMap = {'[Baseline] ','[Mapping 1] ','[Mapping 2] '};
titlestr = {' - No distraction - Yielding',' - No distraction - No yielding',' - Distraction - Yielding',' - Distraction - No yielding'};
j = 0;
k = 1;
figure;
for i = 1:length(meanwatchP2)
    j = j+1;
    if (j >3)
        j = 1;
        figure;
        k = k+1;
    end
    tstr = join([strMap{j},'Mean watch times per phase', titlestr{k}]);
    subplot(3,1,j);
    X = categorical(cats);
    X = reordercats(X,cats);
    Y = [meanwatchP2(i) meanNWP2(i) meaninvalidP2(i); meanwatchP3(i) meanNWP3(i) meaninvalidP3(i); meanwatchP4(i) meanNWP4(i) meaninvalidP4(i)];
    h = bar(X,Y); grid on;
    set(h, {'DisplayName'},{'Watch','No watch','Invalid'}')
    title(tstr); ylabel('Watch time in [s]'); legend();
end
end

function [ND_Y, ND_NY, D_Y, D_NY] = getOrganizedDis(data)
field = fieldnames(data.Data_ED_0.HostFixedTimeLog);
% ND_Y: ED 0, 4, 8
ND_Y(:,1) = data.Data_ED_0.HostFixedTimeLog.(field{:});
ND_Y(:,2) = data.Data_ED_4.HostFixedTimeLog.(field{:});
ND_Y(:,3) = data.Data_ED_8.HostFixedTimeLog.(field{:});

% ND_NY: ED 1, 5, 9
ND_NY(:,1) = data.Data_ED_1.HostFixedTimeLog.(field{:});
ND_NY(:,2) = data.Data_ED_5.HostFixedTimeLog.(field{:});
ND_NY(:,3) = data.Data_ED_9.HostFixedTimeLog.(field{:});

% D_Y: ED 2, 6, 10
D_Y(:,1) = data.Data_ED_2.HostFixedTimeLog.(field{:});
D_Y(:,2) = data.Data_ED_6.HostFixedTimeLog.(field{:});
D_Y(:,3) = data.Data_ED_10.HostFixedTimeLog.(field{:});

% D_NY: ED 1, 5, 9
D_NY(:,1) = data.Data_ED_3.HostFixedTimeLog.(field{:});
D_NY(:,2) = data.Data_ED_7.HostFixedTimeLog.(field{:});
D_NY(:,3) = data.Data_ED_11.HostFixedTimeLog.(field{:});
end
function [val, dis] = sumDis(in) 
part = 10; % group by 1/part
rnd = roundDis(in,part);
fill = fillUpArray(rnd);
[val, dis] = countDis(fill,part);
val = 100*(val/length(in));
end
function out = fun_round(in,part)
up = in<round(in);
out = zeros(size(up));
for i =1:length(up)
    if(up(i) == true)
        out(i) = ceil(in(i)*part)/part;
    elseif(up(i) == false)
        out(i) = floor(in(i)*part)/part;
    end
end
end
function out = roundDis(in,part)
out = cell(size(in));
for j = 1:size(in,2)
	for k = 1:size(in,1)
%         out{k,j} = round(in{k,j},decimals);
        out{k,j} = fun_round(in{k,j},part);
	end
end
end
function out = fillUpArray(in)
[max_size, ~] = max(cellfun('size', in, 1));
x = ceil((max(max_size))/50)*50;
tempout = zeros(x,1);
out = cell(size(in));
for j=1:size(in,2)                          % 3 columns
    for i=1:length(in)                      % 44 rows
        temp = in{i,j};                     % extract 1 cell array (e.g. 1340x1)
        tempout(1:length(temp)) = temp;     % Copy up to 1340
        for di = length(temp)+1:x
            tempout(di) = temp(end);
        end
        out{i,j} = tempout;
    end
end
end
function [val, dis] = countDis(in,part)
dis = (-1:(1/part):70)';
val = zeros(length(dis),size(in,2));
for col = 1:size(in,2)
    for row = 1:size(in,1)
        [GC, GR] = groupcounts(in{row,col});         % One cell array
        for i = 1:length(dis)
            index = find(GR==dis(i));                % index of value
            if(~isempty(index))
                val(i,col) = val(i,col) + 1; %GC(index); % add occurence of value
            end
        end
    end
end
end
function visPasGazePed(val,name,person,mode,val2,name2,person2,mode2)
% String arrays to select from
strMap = {'[Baseline] ','[Mapping 1] ','[Mapping 2] '};
titlestr = {' - No distraction - Yielding',' - No distraction - No yielding',' - Distraction - Yielding',' - Distraction - No yielding'};
modes = {'ND_Y';'ND_NY';'D_Y';'D_NY'};
people = {'pa';'pe'};
titp = {'Distance passenger gazing pedestrian'; 'Distance pedestrian gazing passenger'};
xlab = {'Distance from the pedestrian in [m]'; 'Distance from the passenger in [m]'};
ylab = {'Passengers watching pedestrian in [%]'; 'Pedestrian watching passenger in [%]'};
% Selection indices
m = find(strcmp(modes,mode));
if(isempty(m))
    warning('Wrong mode input');
    return
end
m2 = find(strcmp(modes,mode2));
if(isempty(m2))
    warning('Wrong mode2 input');
    return
end
p = find(strcmp(people,person));
p2 = find(strcmp(people,person2));
% Visualization
figure;
j=0;
for i = 1:2:5
    j = j+1;
    subplot(3,2,i)
    idstart = find(name==0)+1;
    plot(name(idstart:end), val(idstart:end,j));
    grid on; set(gca, 'XDir','reverse');
    title(join([strMap(j), titp(p), titlestr(m)])); 
    xlabel(xlab(p));
    ylabel(ylab(p));
    ylim([0 100]);
    if(m==1||m==3)
        xline(6.24,'-',{'AV standstill'});
    end
    
    subplot(3,2,i+1)
    idstart = find(name2==0)+1;
    plot(name2(idstart:end), val2(idstart:end,j));
    grid on; set(gca, 'XDir','reverse');
    title(join([strMap(j), titp(p2), titlestr(m2)])); 
    xlabel(xlab(p2));
    ylabel(ylab(p2));
    ylim([0 100]);
    if(m2==1||m2==3)
        xline(6.24,'-',{'AV standstill'});
    end
end
end
