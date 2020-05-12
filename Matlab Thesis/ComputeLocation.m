clear all
close all
clc

run LocationCar.m

%Filter out timevalues that are double or exceed bound

Cnt = 1;
for i = 2:length(C)
   if C(i,1) ~= C(i-1,1) && C(i,1) <= 22
      % C(i,:) = [];
     Goodrow(Cnt) = i;
      Cnt = Cnt + 1;
   end
end
% for i = 2:length(C)
%    if C(i,1) == C(i-1,1) && C(i,1) <= 24
%       % C(i,:) = [];
%       DuplicateRows(Cnt) = i;
%       Cnt = Cnt + 1;
%    end
%    if C(i,1) > 24
%        DuplicateRows(Cnt) = i;
%        Cnt = Cnt + 1;
%    end
% end

for i = 1:length(Goodrow)
   x = Goodrow(i);
   Location(i,:) = C(x,:);
   Location(i,1) = Location(i,1)-1;
end

% Find velocity
for i = 2:length(Location)
    Velocity(i-1,1) = Location(i,1);
    Velocity(i-1,2) = ((Location(i,2)-Location(i-1,2))/(Location(i,1)-Location(i-1,1)))*3.6;

end

AVG_velocity = 0;
sumvalue = 200;
for i = 1:sumvalue
   AVG_velocity = AVG_velocity + Velocity(i,2) 
end
AVG_velocity = AVG_velocity/sumvalue

% Find acceleration
for i = 2:length(Velocity)
   Acc(i-1) = (Velocity(i,2)-Velocity(i-1,2))/(Location(i,1)-Location(i-1,1)) 
end

% FiveCnt = 0;
% LocCnt = 0;
% for i = 1:length(Velocity)
%    FiveCnt = FiveCnt + 1; 
%    if FiveCnt == 5;
%        LocCnt = LocCnt + 1;
%        AvgVelocity(LocCnt) = (Velocity(i-4,2)+Velocity(i-3,2)+Velocity(i-2,2)+Velocity(i-1,2)+Velocity(i,2))/5;
%        FiveCnt = 0;
%    end
% end

% Plot data
figure
hold on
plot(Location(:,1),Location(:,2))
plot(8.64,-30,'x')

figure
plot(Velocity(:,1),Velocity(:,2))