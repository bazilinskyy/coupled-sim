%%
% Calculate the point where the distraction vehicle passes the zebra
% crossing

% Only needed for the Distraction - Yielding case
% ED: 2, 6, 10
function out = DiAVPasses(data)
pos_ED2 = data.Data_ED_2.HostFixedTimeLog.participant_1.trial_1.diAV.pos.z;
pos_ED6 = data.Data_ED_6.HostFixedTimeLog.participant_1.trial_1.diAV.pos.z;
pos_ED10 = data.Data_ED_10.HostFixedTimeLog.participant_1.trial_1.diAV.pos.z;

pos_2 = data.Data_ED_2.HostFixedTimeLog.participant_1.trial_1.pa.pos.z;
pos_ped_2 = data.Data_ED_2.HostFixedTimeLog.participant_1.trial_1.pe.pos.z;

x2 = find(pos_ED2 > 17.19,1,'first');
x6 = find(pos_ED6 > 17.19,1,'first');
x10 = find(pos_ED10 > 17.19,1,'first');

out = x2;
end

