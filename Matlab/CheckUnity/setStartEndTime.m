function out = setStartEndTime(data)
fld_trial = fieldnames(data);
for i=1:length(fld_trial)
    id_start = find(data.(fld_trial{i}).pa.pos.z < 75.5,1,'first');
    id_end = find(data.(fld_trial{i}).pa.pos.z < 14,1,'first');
    
    out.(fld_trial{i}).time = data.(fld_trial{i}).Time(id_start:id_end);
    out.(fld_trial{i}).pos = data.(fld_trial{i}).pa.pos.z(id_start:id_end);
    out.(fld_trial{i}).vel = data.(fld_trial{i}).pa.world.rb_v.z(id_start:id_end);
    out.(fld_trial{i}).resume = data.(fld_trial{i}).resume(id_start:id_end);
end
end