%% GazeTimePlotter
% This script vizualizes the gaze data from the pedestrian and the
% passenger
% Author: Johnson Mok
% Last Updated: 04-02-2021

function gazeTimePlotter(in)
%% EyeContact 
visEyeContact(in.EC);

%% Passenger gaze time - Full trial
visFullWatch(in.pa_full_watch); 

%% Passenger gazing at pedestrian during phases 
% Visualization of passenger gazing at pedestrian during phases 
visPhaseWatch(in.wP2.ND_Y,  in.wP3.ND_Y,    in.wP4.ND_Y,    in.wP2.D_Y, in.wP3.D_Y, in.wP4.D_Y, "yield");
visPhaseWatch(in.wP2.ND_NY, in.wP3.ND_NY,   in.wP4.ND_NY,   in.wP2.D_NY,in.wP3.D_NY,in.wP4.D_NY,"no");

% Bar visualization of mean
visMeanWatchtype(in.mean_wP2, in.mean_wP3, in.mean_wP4, in.mean_invP2, in.mean_invP3, in.mean_invP4, in.mean_nwP2, in.mean_nwP3, in.mean_nwP4);

%% Number of people watching at distance z. Divided by phases
visPasGazePed(in.pa_val.ND_Y,  in.pa_name.ND_Y,  'pa', 'ND_Y',  in.pa_val.D_Y,  in.pa_name.D_Y,  'pa', 'D_Y');
visPasGazePed(in.pa_val.ND_NY, in.pa_name.ND_NY, 'pa', 'ND_NY', in.pa_val.D_NY, in.pa_name.D_NY, 'pa', 'D_NY');
visPasGazePed(in.pe_val.ND_Y,  in.pe_name.ND_Y,  'pe', 'ND_Y',  in.pe_val.D_Y,  in.pe_name.D_Y,  'pe', 'D_Y');
visPasGazePed(in.pe_val.ND_NY, in.pe_name.ND_NY, 'pe', 'ND_NY', in.pe_val.D_NY, in.pe_name.D_NY, 'pe', 'D_NY');

end

%% Helper functions
function out = ceilHalf(in)
 out = floor(in) + ceil((in-floor(in))/0.5)*0.5;
end
function out = calcYlim(in1, in2)
fld = fieldnames(in1);
max1 = zeros(1,3);
max2 = zeros(1,3);
for i = 1:length(fld)
    max1(i) = max(in1.(fld{i}),[],'all');
    max2(i) = max(in2.(fld{i}),[],'all');
end
largest = max([max1, max2]);
out = ceilHalf(largest);
end
function [C, grp] = adjustForBox(data)
fld = fieldnames(data);
C = [data.(fld{1});data.(fld{2});data.(fld{3})];
grp = [ones(size(data.(fld{1}))); 2*ones(size(data.(fld{2}))); 3*ones(size(data.(fld{3})))];
end

function visEyeContact(EC)
strMap = {'Baseline','Mapping 1','Mapping 2'};
lim1 = calcYlim(EC.ND_Y, EC.D_Y);
lim2 = calcYlim(EC.ND_NY, EC.D_NY);
figure;
hold on;
subplot(2,2,1)
[C,grp] = adjustForBox(EC.ND_Y);
boxplot(C,grp,'Labels',strMap);
grid on; ylim([-0.5 lim1]);
ylabel('Eye Contact in [s]'); title('Eye contact - No distraction - Yielding');

subplot(2,2,3)
[C,grp] = adjustForBox(EC.ND_NY);
boxplot(C,grp,'Labels',strMap);
grid on; ylim([-0.5 lim2]);
ylabel('Eye Contact in [s]'); title('Eye contact - No distraction - No yielding');

subplot(2,2,2)
[C,grp] = adjustForBox(EC.D_Y);
boxplot(C,grp,'Labels',strMap);
grid on; ylim([-0.5 lim1]);
ylabel('Eye Contact in [s]'); title('Eye contact - Distraction - Yielding');

subplot(2,2,4)
[C,grp] = adjustForBox(EC.D_NY);
boxplot(C,grp,'Labels',strMap);
grid on; ylim([-0.5 lim2]);
ylabel('Eye Contact in [s]'); title('Eye contact - Distraction - No yielding');
end

function visFullWatch(pa_full_watch)
strMap = {'Baseline','Mapping 1','Mapping 2'};
titlestr = '[Full run]';

lim1 = calcYlim(pa_full_watch.ND_Y, pa_full_watch.D_Y);
lim2 = calcYlim(pa_full_watch.ND_NY, pa_full_watch.D_NY);

figure;
subplot(2,2,1)
[C,grp] = adjustForBox(pa_full_watch.ND_Y);
boxplot(C,grp,'Labels',strMap);
grid on; ylim([-0.5 lim1]); 
ylabel('Gaze time in [s]'); title(join([titlestr,' Passenger gazing pedestrian - No distraction - Yielding']));

subplot(2,2,3)
[C,grp] = adjustForBox(pa_full_watch.ND_NY);
boxplot(C,grp,'Labels',strMap);
grid on; ylim([-0.5 lim2]);
ylabel('Gaze time in [s]'); title(join([titlestr,' Passenger gazing pedestrian - No distraction - No yielding']));

subplot(2,2,2)
[C,grp] = adjustForBox(pa_full_watch.D_Y);
boxplot(C,grp,'Labels',strMap);
grid on; ylim([-0.5 lim1]);
ylabel('Gaze time in [s]'); title(join([titlestr,' Passenger gazing pedestrian - Distraction - Yielding']));

subplot(2,2,4)
[C,grp] = adjustForBox(pa_full_watch.D_NY);
boxplot(C,grp,'Labels',strMap);
grid on; ylim([-0.5 lim2]);
ylabel('Gaze time in [s]'); title(join([titlestr,' Passenger gazing pedestrian - Distraction - No yielding']));
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
fld = fieldnames(p2);
figure;
for m1 = 1:length(strMap)
    subplot(3,2,i(m1));
    boxplot([p2.(fld{m1}); p3.(fld{m1}); p4.(fld{m1})],[ones(size(p2.(fld{m1}))); 2*ones(size(p3.(fld{m1}))); 3*ones(size(p4.(fld{m1})))], ...
        'Labels',{'Tracking till AV yield','During yield','After reset'});
    ylabel('Passenger gazing pedestrian time in [s]'); title(join([strMap(m1),tstr1]));

    subplot(3,2,i(m1)+1);
    boxplot([p22.(fld{m1}); p33.(fld{m1}); p44.(fld{m1})],[ones(size(p22.(fld{m1}))); 2*ones(size(p33.(fld{m1}))); 3*ones(size(p44.(fld{m1})))], ...
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
    idstart = find(name==0,1)+1;
    plot(name(idstart:end,j), val(idstart:end,j));
    grid on; set(gca, 'XDir','reverse');
    title(join([strMap(j), titp(p), titlestr(m)])); 
    xlabel(xlab(p));
    ylabel(ylab(p));
    ylim([0 100]);
    if(m==1||m==3)
        xline(6.24,'-',{'AV standstill'});
    end
    
    subplot(3,2,i+1)
    idstart = find(name2==0,1)+1;
    plot(name2(idstart:end,j), val2(idstart:end,j));
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
