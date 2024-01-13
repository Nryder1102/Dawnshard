import React, { FC, ReactNode } from "react";
import { Box, BoxProps, Drawer, ModalClose, Sheet, Typography } from "@mui/joy";
import useBreakpoint from "../../shared/hooks/useBreakpoint.ts";

export const RootLayout: FC<BoxProps> = (props: BoxProps) => {
  return (
    <Box
      {...props}
      sx={[
        {
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "minmax(64px, 200px) minmax(450px, 1fr)",
            md: "minmax(160px, 300px) 1fr",
          },
          gridTemplateRows: "64px 1fr",
          minHeight: "100vh",
        },
        ...(Array.isArray(props.sx) ? props.sx : [props.sx]),
      ]}
    />
  );
};

export const HeaderLayout: FC<BoxProps> = (props: BoxProps) => {
  return (
    <Box
      component="header"
      className="Header"
      {...props}
      sx={[
        {
          p: 2,
          gap: 2,
          bgcolor: "background.surface",
          display: "flex",
          flexDirection: "row",
          justifyContent: "space-between",
          alignItems: "center",
          gridColumn: "1 / -1",
          borderBottom: "1px solid",
          borderColor: "divider",
          position: "sticky",
          top: 0,
          zIndex: 1100,
        },
        ...(Array.isArray(props.sx) ? props.sx : [props.sx]),
      ]}
    />
  );
};

export const SideNavLayout: FC<{
  open: boolean;
  onCloseDrawer: () => void;
  children: ReactNode;
}> = ({ open, onCloseDrawer, children }) => {
  const small = useBreakpoint((breakpoints) => breakpoints.down("sm"));
  console.log({ drawerOpen: open, small: small });

  return small ? (
    <Drawer
      open={open}
      onClose={onCloseDrawer}
      component="nav"
      className="navigation"
    >
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          gap: 0.5,
          ml: "auto",
          mt: 1,
          mr: 2,
        }}
      >
        <Typography
          component="label"
          htmlFor="close-icon"
          fontSize="sm"
          fontWeight="lg"
          sx={{ cursor: "pointer" }}
        >
          Close
        </Typography>
        <ModalClose id="close-icon" sx={{ position: "initial" }} />
      </Box>
      <Box sx={{ padding: "1rem" }}>{children}</Box>
    </Drawer>
  ) : (
    <Box
      component="nav"
      className="Navigation"
      sx={[
        {
          p: 2,
          backgroundColor: "background.surface",
          borderRight: "1px solid",
          borderColor: "divider",
          display: {
            xs: "none",
            sm: "initial",
          },
          position: "sticky",
          height: "100%",
        },
      ]}
    >
      {children}
    </Box>
  );
};

export const SidePaneLayout: FC<BoxProps> = (props: BoxProps) => {
  return (
    <Box
      className="Inbox"
      {...props}
      sx={[
        {
          bgcolor: "background.surface",
          borderRight: "1px solid",
          borderColor: "divider",
          display: {
            xs: "none",
            md: "initial",
          },
        },
        ...(Array.isArray(props.sx) ? props.sx : [props.sx]),
      ]}
    />
  );
};

export const MainLayout: FC<BoxProps> = (props: BoxProps) => {
  return (
    <Box
      component="main"
      className="Main"
      {...props}
      sx={[...(Array.isArray(props.sx) ? props.sx : [props.sx])]}
    />
  );
};
