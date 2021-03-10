%% Analyse Trigger Yield
% This script analyzes the trigger position and the corresponding
% deceleration distance and standstill duration

% Author: Johnson Mok
% Last Updated: 03-03-2021

function analyzeTriggerYield(data)
data_yield = getGazeToYield(data);
data_trigger = getTriggerLocation(data_yield);
totalTime = calcDecelStandstillTime(data_trigger);
visualizeTriggerData3D(data_trigger);
visualizeTotalTime(totalTime)
visualizeTriggerData2D(data_trigger);
end

%% Helper functions
function out = getGazeToYield(data)
% ED 4 and 6
out.ND_Y = data.Data_ED_4.HostFixedTimeLog;
out.D_Y = data.Data_ED_6.HostFixedTimeLog;
end

function out = getTriggerLocation(data)
dt = 0.0167;
out.ND_Y.pos_trigger = [];
out.ND_Y.decel_time = [];
out.ND_Y.standstill_time = [];
out.ND_Y.person = [];

out.D_Y.pos_trigger = [];
out.D_Y.decel_time = [];
out.D_Y.standstill_time = [];
out.D_Y.person = [];

fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_par = fieldnames(data.(fld_con{c}));
    for par =1:length(fld_par)
        fld_trial = fieldnames(data.(fld_con{c}).(fld_par{par}));
        for tr=1:length(fld_trial)
            % Get indices
            data_dis = data.(fld_con{c}).(fld_par{par}).(fld_trial{tr}).pa.distance;
            id_trigger = find(data_dis<25.0 & data_dis>14.4 ,1,'first');
            data_pos = data.(fld_con{c}).(fld_par{par}).(fld_trial{tr}).pa.pos.z;
            pos_trigger = data_pos(id_trigger)-17.19;
            data_rbv = abs(data.(fld_con{c}).(fld_par{par}).(fld_trial{tr}).pa.world.rb_v.z);
            data_acc = gradient(data_rbv,10);
            id_decel_end = find(abs(gradient(data_pos))<0.001,1,'first');
            id_restart = find(data_acc>0.2,1,'first');
            
            % Prep label
            partnr = split(fld_par{par},'_');
            trialnr = split(fld_trial{tr},'_');
            person = ['P',partnr{2},'-','T',trialnr{2}];
            
            out.(fld_con{c}).(fld_par{par}).(fld_trial{tr}).pos_trigger = pos_trigger;
            out.(fld_con{c}).(fld_par{par}).(fld_trial{tr}).decel_time = (id_decel_end-id_trigger)*dt;
            out.(fld_con{c}).(fld_par{par}).(fld_trial{tr}).standstill_time = (id_restart-id_decel_end)*dt;
            
            out.(fld_con{c}).pos_trigger = [out.(fld_con{c}).pos_trigger; pos_trigger];
            out.(fld_con{c}).standstill_time = [out.(fld_con{c}).standstill_time; (id_decel_end-id_trigger)*dt];
            out.(fld_con{c}).decel_time = [out.(fld_con{c}).decel_time; (id_restart-id_decel_end)*dt];
            out.(fld_con{c}).person = [out.(fld_con{c}).person; {person}];
            
            % For debugging
            if(false)
                x=(0:length(data_rbv)-1)*dt;
                figure;
                hold on
                subplot(4,1,1)
                plot(x,data_pos)
                xline(id_trigger*dt,'--')
                xline(id_decel_end*dt,'--')
                xline(id_restart*dt,'--')
                title(['trigger location: ', num2str(pos_trigger)]);
                grid on;
                
                subplot(4,1,2)
                plot(x,data_rbv)
                xline(id_trigger*dt,'--')
                xline(id_decel_end*dt,'--')
                xline(id_restart*dt,'--')
                grid on;  
                title(['deceleration duration: ', num2str((id_decel_end-id_trigger)*0.0167)]);
                
                subplot(4,1,3)
                plot(x,gradient(data_acc))
                grid on;
                xline(id_trigger*dt,'--')
                xline(id_decel_end*dt,'--')
                xline(id_restart*dt,'--')
                title(['standstill duration: ', num2str((id_restart-id_decel_end)*0.0167)]);
                
                subplot(4,1,4)
                plot(x,gradient(data_pos))
                grid on;
                xline(id_trigger*dt,'--')
                xline(id_decel_end*dt,'--')
                xline(id_restart*dt,'--')
            end
        end
    end
end

end

function visualizeTriggerData3D(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    startnr = 1;
    endnr = length(data.(fld_con{c}).pos_trigger);
    
    pos_trig = data.(fld_con{c}).pos_trigger(startnr:endnr);
    dec_time = data.(fld_con{c}).decel_time(startnr:endnr);
    ss_time = data.(fld_con{c}).standstill_time(startnr:endnr);
    labels = data.(fld_con{c}).person(startnr:endnr);
    figure;
    subplot(2,2,1)
    plot3(pos_trig, dec_time, ss_time, 'o')
    text(pos_trig,dec_time,ss_time,labels,'VerticalAlignment','bottom','HorizontalAlignment','right')
    xlabel('trigger position in [m]')
    ylabel('deceleration duration in [s]')
    zlabel('standstill duration in [s]')
    title(fld_con{c})
    grid on;
    
    subplot(2,2,2)
    plot3(pos_trig, dec_time, ss_time, 'o')
    text(pos_trig,dec_time,ss_time,labels,'VerticalAlignment','bottom','HorizontalAlignment','right')
    xlabel('trigger position in [m]')
    ylabel('deceleration duration in [s]')
    zlabel('standstill duration in [s]')
    title(fld_con{c})
    grid on;
    view(0,90)
    
    subplot(2,2,3)
    plot3(pos_trig, dec_time, ss_time, 'o')
    text(pos_trig,dec_time,ss_time,labels,'VerticalAlignment','bottom','HorizontalAlignment','right')
    xlabel('trigger position in [m]')
    ylabel('deceleration duration in [s]')
    zlabel('standstill duration in [s]')
    title(fld_con{c})
    grid on;
    view(90,90)
    
    subplot(2,2,4)
    plot3(pos_trig, dec_time, ss_time, 'o')
    text(pos_trig,dec_time,ss_time,labels,'VerticalAlignment','bottom','HorizontalAlignment','right')
    xlabel('trigger position in [m]')
    ylabel('deceleration duration in [s]')
    zlabel('standstill duration in [s]')
    title(fld_con{c})
    grid on;
    view(90,0)
end
end

function out = calcDecelStandstillTime(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    out.(fld_con{c}) = data.(fld_con{c}).decel_time + data.(fld_con{c}).standstill_time;
end
end

function visualizeTotalTime(data)
fld_con = fieldnames(data);

figure
x1 = 1*ones(length(data.ND_Y),1); x2 = 2*ones(length(data.D_Y),1); 
x = [x1;x2];
boxplot([data.ND_Y; data.D_Y],x);
hold on;
plot(1,mean(data.ND_Y),'r*',2,mean(data.D_Y),'r*');
ylabel('duration in [s]');
title('Deceleration time and standstill time combined');
set(gca,'XTick',1:2,'XTickLabel',{'no distraction - yielding','distraction - yielding'});
grid on;
end

function visualizeTriggerData2D(data)
fld_con = fieldnames(data);
figure
for c=1:length(fld_con)
    use = data.(fld_con{c});
    decelTime = use.decel_time;
    ssTime = use.standstill_time;
    person = use.person;
    
    subplot(2,1,c)
    X = categorical(person');
    X = reordercats(X,person');
    bar(X,[decelTime';ssTime'],'stacked');
    grid on;
    xlabel('participant and trial number');
    ylabel('duration in [s]')
    legend('deceleration time','standstill time');
    title(fld_con{c});
end
end



