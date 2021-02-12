%% Visualizee GapAcceptance
% This script vizualizes the gap acceptance data 
% Author: Johnson Mok
% Last Updated: 04-02-2021

function visualizeGapAcceptance(in)
visSumGapAcceptance(in.smoothgap);

visGapAcceptVSother(in.sumGap, in.sumAVvel, 'AV velocity ','in [m/s]', [-0.5 35]);
visGapAcceptVSother(in.sumGap, in.sumAVposz, 'AV z-position ','in [m]', [-50 80]);
               
visGapAcceptVSposped(in.sumGap, in.sumAVposz);
               
end


%% Helper functions
function visSumGapAcceptance(data)
strMap = {'Baseline','Mapping 1','Mapping 2'};
dt = 0.0167;
fld_con = fieldnames(data);
titlestr = {'Gap Acceptance - No Distraction - Yielding';'Gap Acceptance - No Distraction - No yielding';...
    'Gap Acceptance - Distraction - Yielding'; 'Gap Acceptance - Distraction - No yielding'};
figure;
for i = 1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{i}));
    subplot(2,2,i)
    hold on;
    x = (1:length(data.(fld_con{i}).(fld_map{1}).gapAcceptance))*dt;
    plot(x,[data.(fld_con{i}).(fld_map{1}).gapAcceptance, data.(fld_con{i}).(fld_map{2}).gapAcceptance, data.(fld_con{i}).(fld_map{3}).gapAcceptance],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('gap acceptance in [%]'); 
    ylim([-0.5 105]);
    legend(strMap); title(titlestr{i});
end
end

function visGapAcceptVSother(data, v, ystr, ystrUnit, ylimit)
strMap = {'Baseline','Mapping 1','Mapping 2'};
dt = 0.0167;
gapstr = 'Gap Acceptance ';
ylab = join([ystr,ystrUnit]);
titlestr = {'- No Distraction - Yielding';'- No Distraction - No yielding';...
    '- Distraction - Yielding'; '- Distraction - No yielding'};

fld_con = fieldnames(data);
fld_map = fieldnames(data.(fld_con{1}));
fld_val = fieldnames(data.(fld_con{1}).(fld_map{1}));
fld_conv = fieldnames(v);
fld_mapv = fieldnames(v.(fld_conv{1}));
fld_valv = fieldnames(v.(fld_conv{1}).(fld_mapv{1}));
for c = 1:2
    figure
    subplot(2,2,1)
    x = (1:length(data.(fld_con{c}).(fld_map{1}).(fld_val{1})))*dt;
    plot(x,[data.(fld_con{c}).(fld_map{1}).(fld_val{1}), data.(fld_con{c}).(fld_map{2}).(fld_val{1}), data.(fld_con{c}).(fld_map{3}).(fld_val{1})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('gap acceptance in [%]'); 
    ylim([-0.5 105]);
    legend(strMap); title(join([gapstr,titlestr{c}]));
    
    subplot(2,2,2)
    x = (1:length(data.(fld_con{c+2}).(fld_map{1}).(fld_val{1})))*dt;
    plot(x,[data.(fld_con{c+2}).(fld_map{1}).(fld_val{1}), data.(fld_con{c+2}).(fld_map{2}).(fld_val{1}), data.(fld_con{c+2}).(fld_map{3}).(fld_val{1})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel('gap acceptance in [%]'); 
    ylim([-0.5 105]);
    legend(strMap); title(join([gapstr,titlestr{c+2}]));
    
    subplot(2,2,3)
    xv = (1:length(v.(fld_conv{c}).(fld_mapv{1}).(fld_valv{1})))*dt;
    plot(xv,[v.(fld_conv{c}).(fld_mapv{1}).(fld_valv{1}), v.(fld_conv{c}).(fld_mapv{2}).(fld_valv{1}), v.(fld_conv{c}).(fld_mapv{3}).(fld_valv{1})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel(ylab);
    ylim(ylimit);
    legend(strMap); title(join([ystr,titlestr{c}]));
    
    subplot(2,2,4)
    xv = (1:length(v.(fld_conv{c+2}).(fld_mapv{1}).(fld_valv{1})))*dt;
    plot(xv,[v.(fld_conv{c+2}).(fld_mapv{1}).(fld_valv{1}), v.(fld_conv{c+2}).(fld_mapv{2}).(fld_valv{1}), v.(fld_conv{c+2}).(fld_mapv{3}).(fld_valv{1})],'LineWidth',2);
    grid on; xlabel('time in [s]'), ylabel(ylab);
    ylim(ylimit);
    legend(strMap); title(join([ystr,titlestr{c+2}]));
end
end

function visGapAcceptVSposped(data,v)
strMap = {'Baseline','Mapping 1','Mapping 2'};
gapstr = 'Gap Acceptance ';
titlestr = {'- No Distraction - Yielding';'- No Distraction - No yielding';...
    '- Distraction - Yielding'; '- Distraction - No yielding'};
fld_con = fieldnames(data);
fld_map = fieldnames(data.(fld_con{1}));
fld_val = fieldnames(data.(fld_con{1}).(fld_map{1}));
fld_conv = fieldnames(v);
fld_mapv = fieldnames(v.(fld_conv{1}));
fld_valv = fieldnames(v.(fld_conv{1}).(fld_mapv{1}));

posped = 17.19;
figure;
for c = 1:length(fld_con)
    subplot(2,2,c);
    hold on;
    for m=1:length(fld_map)
        x = v.(fld_conv{c}).(fld_mapv{m}).(fld_valv{1})-posped;
        plot(x,data.(fld_con{c}).(fld_map{m}).(fld_val{1}),'LineWidth',2);
    end
    set(gca, 'XDir','reverse'); ylim([0 100]);
    xlabel('Distance till pedestrian in [m]'); ylabel('gap acceptance in [%]');
    title(join([gapstr,titlestr{c}]));
    legend(strMap); grid on; 
end
end
