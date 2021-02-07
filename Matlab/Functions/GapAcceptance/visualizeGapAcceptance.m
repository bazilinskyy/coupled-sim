%% Visualizee GapAcceptance
% This script vizualizes the gap acceptance data 
% Author: Johnson Mok
% Last Updated: 04-02-2021

function visualizeGapAcceptance(in)
visSumGapAcceptance(in.smoothgap.ND_Y, in.smoothgap.ND_NY, in.smoothgap.D_Y, in.smoothgap.D_NY);

visGapAcceptVSrbv(in.sumGap.ND_Y,   in.sumGap.ND_NY,   in.sumGap.D_Y,   in.sumGap.D_NY,...
                  in.sumAVvel.ND_Y, in.sumAVvel.ND_NY, in.sumAVvel.D_Y, in.sumAVvel.D_NY);
              
visGapAcceptVSpasz(in.sumGap.ND_Y,   in.sumGap.ND_NY,   in.sumGap.D_Y,   in.sumGap.D_NY,...
                   in.sumAVposz.ND_Y, in.sumAVposz.ND_NY, in.sumAVposz.D_Y, in.sumAVposz.D_NY);
               
visGapAcceptVSposped(in.sumGap.ND_Y,   in.sumGap.ND_NY,   in.sumGap.D_Y,   in.sumGap.D_NY,...
                   in.sumAVposz.ND_Y, in.sumAVposz.ND_NY, in.sumAVposz.D_Y, in.sumAVposz.D_NY);
               
end


%% Helper functions
function visSumGapAcceptance(sum_ND_Y,sum_ND_NY,sum_D_Y,sum_D_NY)
strMap = {'Baseline','Mapping 1','Mapping 2'};
dt = 0.0167;
data = {sum_ND_Y,sum_ND_NY,sum_D_Y,sum_D_NY}; 
titlestr = {'Gap Acceptance - No Distraction - Yielding';'Gap Acceptance - No Distraction - No yielding';...
    'Gap Acceptance - Distraction - Yielding'; 'Gap Acceptance - Distraction - No yielding'};
figure;
for i = 1:length(data)
    fld = fieldnames(data{i});
    subplot(2,2,i)
    hold on;
    x = (1:length(data{i}.(fld{1})))*dt;
    plot(x,[data{i}.(fld{1}), data{i}.(fld{2}), data{i}.(fld{3})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('gap acceptance in [%]'); 
    ylim([-0.5 105]);
    legend(strMap); title(titlestr{i});
end
end
function visGapAcceptVSrbv(sum_ND_Y,sum_ND_NY,sum_D_Y,sum_D_NY, SvND_Y, SvND_NY, SvD_Y, SvD_NY)
strMap = {'Baseline','Mapping 1','Mapping 2'};
dt = 0.0167;
data = {sum_ND_Y,sum_ND_NY,sum_D_Y,sum_D_NY}; 
v = {SvND_Y, SvND_NY, SvD_Y, SvD_NY};
gapstr = 'Gap Acceptance ';
velstr = 'AV velocity ';
titlestr = {'- No Distraction - Yielding';'- No Distraction - No yielding';...
    '- Distraction - Yielding'; '- Distraction - No yielding'};
for i = 1:2
    fld = fieldnames(data{i});
    figure
    subplot(2,2,1)
    x = (1:length(data{i}.(fld{1})))*dt;
    plot(x,[data{i}.(fld{1}), data{i}.(fld{2}), data{i}.(fld{3})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('gap acceptance in [%]'); 
    ylim([-0.5 105]);
    legend(strMap); title(join([gapstr,titlestr{i}]));
    
    subplot(2,2,2)
    x = (1:length(data{i+2}.(fld{1})))*dt;
    plot(x,[data{i+2}.(fld{1}), data{i+2}.(fld{2}), data{i+2}.(fld{3})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('gap acceptance in [%]'); 
    ylim([-0.5 105]);
    legend(strMap); title(join([gapstr,titlestr{i+2}]));
    
    subplot(2,2,3)
    xv = (1:length(v{i}.(fld{1})))*dt;
    plot(xv,[v{i}.(fld{1}), v{i}.(fld{2}), v{i}.(fld{3})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('AV velocity in [m/s]');
    ylim([-0.5 30.5]);
    legend(strMap); title(join([velstr,titlestr{i}]));
    
    subplot(2,2,4)
    xv = (1:length(v{i+2}.(fld{1})))*dt;
    plot(xv,[v{i+2}.(fld{1}), v{i+2}.(fld{2}), v{i+2}.(fld{3})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('AV velocity in [m/s]');
    ylim([-0.5 30.5]);
    legend(strMap); title(join([velstr,titlestr{i+2}]));
end
end
function visGapAcceptVSpasz(sum_ND_Y,sum_ND_NY,sum_D_Y,sum_D_NY, paszND_Y, paszND_NY, paszD_Y, paszD_NY)
strMap = {'Baseline','Mapping 1','Mapping 2'};
dt = 0.0167;
data = {sum_ND_Y,sum_ND_NY,sum_D_Y,sum_D_NY}; 
v = {paszND_Y, paszND_NY, paszD_Y, paszD_NY};
gapstr = 'Gap Acceptance ';
velstr = 'AV z-position ';
titlestr = {'- No Distraction - Yielding';'- No Distraction - No yielding';...
    '- Distraction - Yielding'; '- Distraction - No yielding'};
for i = 1:2
    fld = fieldnames(data{i});
    figure
    subplot(2,2,1)
    x = (1:length(data{i}.(fld{1})))*dt;
    plot(x,[data{i}.(fld{1}), data{i}.(fld{2}), data{i}.(fld{3})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('gap acceptance in [%]'); 
    ylim([-0.5 105]);
    legend(strMap); title(join([gapstr,titlestr{i}]));
    
    subplot(2,2,2)
    x = (1:length(data{i+2}.(fld{1})))*dt;
    plot(x,[data{i+2}.(fld{1}), data{i+2}.(fld{2}), data{i+2}.(fld{3})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('gap acceptance in [%]'); 
    ylim([-0.5 105]);
    legend(strMap); title(join([gapstr,titlestr{i+2}]));
    
    subplot(2,2,3)
    xv = (1:length(v{i}.(fld{1})))*dt;
    plot(xv,[v{i}.(fld{1}), v{i}.(fld{2}), v{i}.(fld{3})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('AV z-position in [m]');
    yline(17.19, '-.b','Pedestrian pos','LineWidth',2);
%     ylim([-0.5 30.5]);
    legend(strMap); title(join([velstr,titlestr{i}]));
    
    subplot(2,2,4)
    xv = (1:length(v{i+2}.(fld{1})))*dt;
    plot(xv,[v{i+2}.(fld{1}), v{i+2}.(fld{2}), v{i+2}.(fld{3})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('AV z-position in [m]');
    yline(17.19, '-.b','Pedestrian pos','LineWidth',2);
%     ylim([-0.5 30.5]);
    legend(strMap); title(join([velstr,titlestr{i+2}]));
end
end
function visGapAcceptVSposped(sum_ND_Y,sum_ND_NY,sum_D_Y,sum_D_NY,paszND_Y, paszND_NY, paszD_Y, paszD_NY)
strMap = {'Baseline','Mapping 1','Mapping 2'};
gapstr = 'Gap Acceptance ';
titlestr = {'- No Distraction - Yielding';'- No Distraction - No yielding';...
    '- Distraction - Yielding'; '- Distraction - No yielding'};
data = {sum_ND_Y,sum_ND_NY,sum_D_Y,sum_D_NY}; 
v = {paszND_Y, paszND_NY, paszD_Y, paszD_NY};
posped = 17.19;
fld = fieldnames(data{1,1});

figure;
for i = 1:length(data)
    subplot(2,2,i);
    hold on;
    for j=1:length(fld)
        x = v{i}.(fld{j})-posped;
        plot(x,data{i}.(fld{j}),'LineWidth',2);
    end
    set(gca, 'XDir','reverse'); ylim([0 100]);
    xlabel('Distance till pedestrian in [m]'); ylabel('gap acceptance in [%]');
    title(join([gapstr,titlestr{i}]));
    legend(strMap); grid on; 
end
end
