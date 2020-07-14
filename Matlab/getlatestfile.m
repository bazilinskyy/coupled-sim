function latestfile = getlatestfile(directory)
%This function returns the latest file from the directory passsed as input
%argument

%Get the directory contents
dirc = dir(directory);

%Filter out all the folders.
dirc = dirc(find(~cellfun(@isdir,{dirc(:).name})));

%I contains the index to the biggest number which is the latest file
[A,I] = max([dirc(:).datenum]);

if ~isempty(I)
    latestfile = dirc(I).name;
end

end