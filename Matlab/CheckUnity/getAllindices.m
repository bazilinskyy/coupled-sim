
function out = getAllindices(data)
fld_trial = fieldnames(data);
id_trigger = zeros(length(fld_trial),1);
id_decel_end = zeros(length(fld_trial),1);
id_resume_first = zeros(length(fld_trial),1);
id_resume_last = zeros(length(fld_trial),1);
t_standstill = zeros(length(fld_trial),1);
t_standstillV2 = zeros(length(fld_trial),1);

trialnr = 1:length(fld_trial);

for i=1:length(fld_trial)
    [id_trigger(i), id_decel_end(i), id_resume_first(i), id_resume_last(i), t_standstill(i), t_standstillV2(i)] = getIndices(data.(fld_trial{i}));           
end

out = [trialnr', id_trigger, id_decel_end, id_resume_first,id_resume_last, t_standstill, t_standstillV2];
end

%% HelperFunction
function [id_trigger, id_decel_end, id_resume_first, id_resume_last, duration_standstill, duration_V2] = getIndices(data)
dt = 0.02;

data_rbv = abs(data.vel);
id_trigger = find(data_rbv<29 & data.pos<50 ,1,'first');%find(data_dis<25.0 & data_dis>14.4 ,1,'first');

data_acc = gradient(data_rbv,10);

id_decel_end = find(abs(gradient(data.pos))<0.001,1,'first');
id_restart = find(data_acc>0.2,1,'first');

id_resume_first = find(data.resume >0,1,'first');
id_resume_last = find(data.resume >0,1,'last');
% id_resume_last = find(data_acc>0.2,1,'first');

duration_standstill = (id_resume_last-id_resume_first)*dt;
duration_V2 = (id_resume_last-id_decel_end)*dt;

% test getPhase script
dx = abs(gradient(data.pos));
id_standstill_start = find(dx<0.01, 1,'first');

            % For debugging
            if(true)
                figure;
                subplot(2,1,1)
                plot(data.pos);
                hold on
                plot(data.vel);
                plot(data.resume);
                xline(id_decel_end,'r--');
                xline(id_standstill_start,'go-');
                xline(id_resume_first,'g--');
                xline(id_resume_last,'b--');
                yline(data.pos(id_resume_first),'--');
                title((id_resume_last-id_resume_first)*0.02);
                grid on;
                subplot(2,1,2)
                plot(data_acc);
                xline(id_decel_end,'r--');
                xline(id_resume_first,'g--');
                xline(id_resume_last,'b--');
            end
            if(false)
                x=(0:length(data_rbv)-1)*dt;
                figure;
                hold on
                subplot(4,1,1)
                plot(x,data.pos)
                xline(id_trigger*dt,'--')
                xline(id_decel_end*dt,'--')
                xline(id_restart*dt,'--')
                xline(id_resume*dt,'r--')
                title(['POS; trigger location: ', num2str(pos_trigger)]);
                grid on;
                
                subplot(4,1,2)
                plot(x,data_rbv)
                xline(id_trigger*dt,'--')
                xline(id_decel_end*dt,'--')
                xline(id_restart*dt,'--')
                grid on;  
                title(['VEL; deceleration duration: ', num2str((id_decel_end-id_trigger)*0.0167)]);
                
                subplot(4,1,3)
                plot(x,gradient(data_acc))
                grid on;
                xline(id_trigger*dt,'--')
                xline(id_decel_end*dt,'--')
                xline(id_restart*dt,'--')
                title(['DDV; standstill duration: ', num2str((id_restart-id_decel_end)*0.0167)]);
                
                subplot(4,1,4)
                plot(x,abs(gradient(data.pos)))
                grid on;
                xline(id_trigger*dt,'--')
                xline(id_decel_end*dt,'--')
                xline(id_restart*dt,'--')
                title(['dp; wait resume duration: ', num2str((id_restart-id_resume)*0.0167)]);
            end
end