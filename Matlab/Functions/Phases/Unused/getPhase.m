%% Get Phases
% This script calculates all the gazing times.
% Hierarchy: calcPhases -> getPhases 

% 1)start sound till start trigger range
% 2)start trigger range till end trigger range
% 3)end trigger range till standstill location AV
% 4)standstill
% 5)standstill location AV till past the zebra crossing

% Author: Johnson Mok
% Last Updated: 05-02-2021

function out = getPhase(data, ED)
out.idx = getPhaseIdx(data,ED);
out.time = getPhaseTime(data,out.idx);
out.pos = getPhasePosTime(data,out.idx);
end

%% Helper functions
function idx = getPhaseIdx(data,ED)
ED_split = split(ED,'_');
EDnr = str2double(ED_split{3}); % ED 4 and 6 use velocity to determine restart, the other yielding ED use 2.6 seconds since standstill
NY = [1, 3, 5, 7, 9, 11];

pos = data.pa.pos.z;
pos_rel = pos - 17.19;
dx = abs(gradient(pos_rel));

% indices
idx_start = 1;
idx_trigger_start = find(pos_rel<25, 1,'first');
idx_trigger_end = find(pos_rel<14.4, 1,'first');
idx_standstill_start = find(dx<0.01, 1,'first');
% disp(['ED',num2str(EDnr),' if1 ' num2str(EDnr == 4),' if2 ', num2str(EDnr == 6)])
if (EDnr == 4 || EDnr == 6)
    if(find(dx<0.001, 1,'last')<find(pos_rel<-4.69, 1,'first'))
        idx_standstill_end = find(dx<0.001, 1,'last');
    end
    if(find(dx<0.001, 1,'last')>find(pos_rel<-4.69, 1,'first'))
        idx_standstill_end = find(dx<0.0001, 1,'last');
    end
    if(find(dx<0.0001, 1,'last')>find(pos_rel<-4.69, 1,'first'))        
        idx_standstill_end = find(dx<0.00001, 1,'last');
    end
else
    idx_standstill_end = idx_standstill_start + round(2.6/0.0167);
end
idx_end = find(pos_rel<-4.69, 1,'first');

% Set up phases
ph1 = [idx_start; idx_trigger_start];
ph2 = [idx_trigger_start; idx_trigger_end];
ph3 = [idx_trigger_end; idx_standstill_start];
if(isempty(find(NY==EDnr)))
    ph4 = [idx_standstill_start; idx_standstill_end];
    ph5 = [idx_standstill_start; idx_end];
else
    ph4 = [];
    ph5 = [];
end
% ph1 = [1; find(pos_rel<25, 1,'first')];                             % Phase 1, start till start trigger range
% ph2 = [find(pos_rel<25, 1,'first'); find(pos_rel<14.4, 1,'first')]; % Phase 2, trigger range
% ph3 = [find(pos_rel<14.4, 1,'first'); find(dx<0.01, 1,'first')];    % Phase 3, end trigger range till standstill
% ph4 = [];
% ph5 = [];
% if(find(dx<0.001, 1,'last')<find(pos_rel<-4.69, 1,'first'))
%     ph4 = [find(dx<0.001, 1,'first'); find(dx<0.001, 1,'last')];          % Phase 4, AV standstill
%     ph5 = [find(dx<0.001, 1,'last'); find(pos_rel<-4.69, 1,'first')];    % Phase 5, AV start driving again till the AV is completely past the zebra crossing
% end
% if(find(dx<0.001, 1,'last')>find(pos_rel<-4.69, 1,'first'))
%     ph4 = [find(dx<0.01, 1,'first'); find(dx<0.0001, 1,'last')];          
%     ph5 = [find(dx<0.0001, 1,'last'); find(pos_rel<-4.69, 1,'first')];
% end
% if(find(dx<0.0001, 1,'last')>find(pos_rel<-4.69, 1,'first'))
%     ph4 = [find(dx<0.01, 1,'first'); find(dx<0.00001, 1,'last')];          
%     ph5 = [find(dx<0.00001, 1,'last'); find(pos_rel<-4.69, 1,'first')];
% end

% Set up idx array
if(~isempty(ph4))% Case: Yielding
    idx = [ph1, ph2, ph3, ph4, ph5];
elseif(isempty(ph4)) % Case: Non yielding
    phNY = [find(pos_rel<14.4, 1,'first'); find(pos_rel<-4.69, 1,'first')]; % Phase NY, end trigger range till the AV is completely past the zebra crossing
    idx = [ph1, ph2, phNY];
end
% Testing
if(false)
    figure;
    grid on;
    plot(pos_rel)
    xline(idx_trigger_start,'-',{'trigger start'});
    xline(idx_trigger_end,'-',{'trigger end'});
    if(~isempty(idx_standstill_start))
        xline(idx_standstill_start,'-',{'standstill start'});
        xline(idx_standstill_end,'-',{'standstill end'});
        xline(idx_end,'-',{'end'});
    end
    title(['standstill time = ', num2str((idx_standstill_end-idx_standstill_start)*0.0167)]);
end
end

function time = getPhaseTime(data,idx)
time = zeros(size(idx));
for i = 1:length(idx)
    time(:,i) = idx(:,i)*data.dt;
end
end

function out = getPhasePosTime(data,idx)
pos = data.pa.pos.z;
pos_rel = pos - 17.19;
i1 = idx(1,1):idx(2,1);
i2 = idx(1,2):idx(2,2);
i3 = idx(1,3):idx(2,3);

out.ph1 = [i1'*data.dt, pos_rel(i1)];
out.ph2 = [i2'*data.dt, pos_rel(i2)];
if(length(idx)>3) 
    i4 = idx(1,4):idx(2,4);
    i5 = idx(1,5):idx(2,5);
    out.ph3 = [i3'*data.dt, pos_rel(i3)];
    out.ph4 = [i4'*data.dt, pos_rel(i4)];
    out.ph5 = [i5'*data.dt, pos_rel(i5)];
else
    out.phNY = [i3'*data.dt, pos_rel(i3)];
end
end





