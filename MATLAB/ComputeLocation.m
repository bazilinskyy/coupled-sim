clear all
close all
clc

run LocationCarYieldBadTime.m
run LocationCarContinue.m

%Filter out timevalues that are double or exceed bound for Yield
Cnt = 1;
for i = 2:length(C_y)
   if C_y(i,1) ~= C_y(i-1,1) && C_y(i,1) <= 22
      % C_y(i,:) = [];
     GoodrowYield(Cnt) = i;
      Cnt = Cnt + 1;
   end
end

Cnt = 1;
for i = 2:length(C_c)
   if C_c(i,1) ~= C_c(i-1,1) && C_c(i,1) <= 13
      % C_y(i,:) = [];
     GoodrowContinue(Cnt) = i;
      Cnt = Cnt + 1;
   end
end

% for i = 2:length(C_y)
%    if C_y(i,1) == C_y(i-1,1) && C_y(i,1) <= 24
%       % C_y(i,:) = [];
%       DupliC_yateRows(Cnt) = i;
%       Cnt = Cnt + 1;
%    end
%    if C_y(i,1) > 24
%        DupliC_yateRows(C_ynt) = i;
%        Cnt = Cnt + 1;
%    end
% end


for i = 1:length(GoodrowYield)
   x = GoodrowYield(i);
   LocationYield(i,:) = C_y(x,:);
   LocationYield(i,1) = LocationYield(i,1)-1;
end


for i = 1:length(GoodrowContinue)
   x = GoodrowContinue(i);
   LocationContinue(i,:) = C_c(x,:);
   LocationContinue(i,1) = LocationContinue(i,1)-1;
end

% % Find veloC_yity
% for i = 2:length(Location)
%     Velocity(i-1,1) = Location(i,1);
%     Velocity(i-1,2) = ((Location(i,2)-Location(i-1,2))/(Location(i,1)-Location(i-1,1)))*3.6;
% 
% end
% 
% AVG_velocity = 0;
% sumvalue = 200;
% for i = 1:sumvalue
%    AVG_velocity = AVG_velocity + Velocity(i,2); 
% end
% AVG_velocity = AVG_velocity/sumvalue;

% % Find acceleration
% for i = 2:length(Velocity)
%    Acc(i-1) = (Velocity(i,2)-Velocity(i-1,2))/(Location(i,1)-Location(i-1,1)) 
% end

% Plot data
figure
hold on
plot(LocationYield(:,1),-LocationYield(:,2))
plot(LocationContinue(:,1),-LocationContinue(:,2))
% plot(8.64,30,'x')
plot([8.64 8.64],[-10 150],'r:')
plot([12.64 12.64],[-10 150],'r:')
plot([16.14 16.14],[-10 150],'r:')
plot([17.64 17.64],[-10 150],'r:')
axis([0 21 -10 150])
xlabel('Time [s]')
ylabel('Distance from pedestrian [m]')
title('Behaviour of car over time')
legend('Yield trajectory','Continue trajectory')
axis on

figure
hold on
plot([0 8.64 12.64 17.64 21],[50 50 0 0 3.36*2*3.6])
plot([0 11],[50 50])
xlabel('Time [s]')
ylabel('Velocity [km/h]')
title('Velocity of car over time')
legend('Yield trajectory','Continue trajectory')
axis on
axis([0 21 -5 55])

% figure
% plot(Velocity(:,1),Velocity(:,2))